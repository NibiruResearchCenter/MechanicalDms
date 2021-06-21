using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using KaiheilaBot.Core.Extension;
using KaiheilaBot.Core.Models.Requests.ChannelMessage;
using KaiheilaBot.Core.Services.IServices;
using MechanicalDms.AccountManager.Helpers;
using MechanicalDms.AccountManager.Models;
using MechanicalDms.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;
using RestSharp;

namespace MechanicalDms.AccountManager.ScheduledJobs
{
    public class FetchBilibiliGuardList : IJob
    {
        public static IHttpApiRequestService HttpApiRequestService { get; set; }
        public static ILogger<IPlugin> Logger { get; set; }

        private static double successRate = 0;

        public async Task Execute(IJobExecutionContext context)
        {
            var watch = new Stopwatch();
            watch.Start();
            var list = await GetGuards(); 
            var jsonStr = JsonSerializer.Serialize(list);
            var now = DateTime.Now;
            var timeStr = $"{now:yyyyMMddHHmm}";

            var filePath = Path.Combine(Configuration.PluginPath, "GuardCache", timeStr + ".json");

            var sw = new StreamWriter(filePath); 
            await sw.WriteAsync(jsonStr);
            sw.Close();
            Configuration.LatestGuardCache = Path.Combine(Configuration.PluginPath, "GuardCache", timeStr + ".json");
            var changes = await UpdateDatabaseAndRole(list);
            watch.Stop();
            
            var message = $"MD-AM - 缓存大航海列表成功，成功率 {successRate}%，修改 {changes} 条用户数据，耗时 {watch.ElapsedMilliseconds} 毫秒";
            Logger.LogInformation(message);
            
            await HttpApiRequestService.GetResponse(new CreateMessageRequest()
            {
                ChannelId = Configuration.AdminChannel,
                Content = $"{DateTime.Now} {message}",
                MessageType = 1
            });
        }
        
        private static async Task<List<Guard>> GetGuards()
        {
            var page = 1;
            var response = await GetGuardPageList(page);
            var firstPageRetry = 2;
            while (response is null && firstPageRetry >= 0)
            {
                Logger.LogError($"MD-AM - 缓存大航海列表，获取第 1 页失败，剩余重试次数：{firstPageRetry}");
                firstPageRetry--;
                response = await GetGuardPageList(page);
            }

            if (response is null)
            {
                await HttpApiRequestService.GetResponse(new CreateMessageRequest()
                {
                    ChannelId = Configuration.AdminChannel,
                    Content = $"{DateTime.Now} MD-AM - 缓存大航海列表，获取第一页失败",
                    MessageType = 1
                });
                return null;
            }
            
            var json = JsonDocument.Parse(response).RootElement;
            var totalPages = json.GetProperty("data").GetProperty("info").GetProperty("page").GetInt32();
            var totalGuards = json.GetProperty("data").GetProperty("info").GetProperty("num").GetInt32();

            var top3 = json.GetProperty("data").GetProperty("top3").EnumerateArray();
            var list = top3.Select(g => new Guard()
            {
                Uid = g.GetProperty("uid").GetInt64(), 
                Username = g.GetProperty("username").GetString(), 
                GuardLevel = g.GetProperty("guard_level").GetInt32()
            }).ToList();
            
            while (page <= totalPages)
            {
                if (page != 1)
                {
                    json = JsonDocument.Parse(response!).RootElement;
                }
                
                Logger.LogDebug($"MD-AM - 缓存大航海列表，获取第 {page} 页成功");
                
                list.AddRange(json.GetProperty("data").GetProperty("list").EnumerateArray()
                    .Select(g => new Guard()
                    {
                        Uid = g.GetProperty("uid").GetInt64(), 
                        Username = g.GetProperty("username").GetString(), 
                        GuardLevel = g.GetProperty("guard_level").GetInt32()
                    }));
                page++;
                
                var retry = 2;
                response = await GetGuardPageList(page);
                while (response is null && retry >= 0 && page <= totalPages)
                {
                    Logger.LogError($"MD-AM - 缓存大航海列表，获取第 {page} 页失败，剩余重试次数：{retry}");
                    retry--;
                    response = await GetGuardPageList(page);
                    // ReSharper disable once InvertIf
                    if (response is null && retry == 0)
                    {
                        retry = 2;
                        page++;
                    }
                }
            }
            
            successRate = list.Count / (double) totalGuards * 100;
            
            return list;
        }
        
        private static async Task<string> GetGuardPageList(int page)
        {
            await Task.Delay(1500);
            var client = new RestClient(new Uri("https://api.live.bilibili.com/xlive/app-room/v2/guardTab/topList"));
            var request = new RestRequest(Method.GET);
            request.AddParameter("roomid", Configuration.LiveRoomId);
            request.AddParameter("ruid", Configuration.LiveHostId);
            request.AddParameter("page", page);
            
            var response = await client.ExecuteAsync(request);

            return response.StatusCode != HttpStatusCode.OK ? null : response.Content;
        }

        private static async Task<int> UpdateDatabaseAndRole(IReadOnlyCollection<Guard> guards)
        {
            await using var db = new DmsDbContext();
            var list = db.KaiheilaUsers
                .AsNoTracking()
                .Include(x => x.BilibiliUser)
                .Where(x=> x.BilibiliUser != null)
                .ToList();

            var roles = new List<string>()
            {
                Configuration.GovernorRole,
                Configuration.AdmiralRole,
                Configuration.CaptainRole
            };

            var changes = 0;
                
            foreach (var user in list)
            {
                var uid = user.BilibiliUser.Uid; 
                var current = guards.FirstOrDefault(x => x.Uid == uid);
                
                if (current is null) 
                { 
                    if (user.BilibiliUser.GuardLevel == 0)
                    {
                        Logger.LogDebug($"MD-AM - Bilibili UID = {uid} 大航海等级为 0");
                        continue;
                    }

                    var gl = user.BilibiliUser.GuardLevel;
                    Logger.LogDebug($"MD-AM - Bilibili UID = {uid} 大航海等级将从 {gl} 调整为 0");
                    var role = roles[gl - 1];
                    user.BilibiliUser.GuardLevel = 0;
                    await RoleHelper.RevokeRole(user.Uid, role, HttpApiRequestService);
                    var userRoles = user.Roles.Trim().Split(' ').ToList();
                    userRoles.Remove(role);
                    user.Roles = string.Join(' ', userRoles);
                    db.BilibiliUsers.Update(user.BilibiliUser);
                    db.KaiheilaUsers.Update(user);
                    await db.SaveChangesAsync();
                    changes++;
                    Logger.LogDebug($"MD-AM - 已修改 Bilibili UID = {user.BilibiliUser.Uid} 的大航海等级 {gl} -> 0");
                }
                else if (current.GuardLevel != user.BilibiliUser.GuardLevel)
                {
                    var oldGl = user.BilibiliUser.GuardLevel;
                    var newGl = current.GuardLevel;
                    Logger.LogDebug($"MD-AM - Bilibili UID = {uid} 大航海等级将从 {oldGl} 调整为 {newGl}");
                    var userRoles = user.Roles.Trim().Split(' ').ToList();
                    if (oldGl != 0)
                    {
                        var oldRole = roles[oldGl - 1];
                        await RoleHelper.RevokeRole(user.Uid, oldRole, HttpApiRequestService);
                        userRoles.Remove(oldRole);
                    }
                    var newRole = roles[newGl - 1];
                    await RoleHelper.GrantRole(user.Uid, newRole, HttpApiRequestService);
                    userRoles.Add(newRole);
                    user.Roles = string.Join(' ', userRoles);
                    user.BilibiliUser.GuardLevel = current.GuardLevel;
                    db.BilibiliUsers.Update(user.BilibiliUser);
                    db.KaiheilaUsers.Update(user);
                    await db.SaveChangesAsync(); 
                    changes++; 
                    Logger.LogDebug(
                        $"MD-AM - 已修改 Bilibili UID = {user.BilibiliUser.Uid} 的大航海等级 {oldGl} -> {newGl}");
                }
            }
            return changes;
        }
    }
}
