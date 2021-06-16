using System;
using System.Collections.Generic;
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
using MechanicalDms.Database.Models;
using MechanicalDms.Operation;
using Microsoft.Extensions.Logging;
using Quartz;
using RestSharp;

namespace MechanicalDms.AccountManager.ScheduledJobs
{
    public class FetchBilibiliGuardList : IJob
    {
        public static IHttpApiRequestService HttpApiRequestService { get; set; }
        public static ILogger<IPlugin> Logger { get; set; }

        public async Task Execute(IJobExecutionContext context)
        {
            var list = await GetGuards(); 
            var jsonStr = JsonSerializer.Serialize(list);
            var now = DateTime.Now;
            var timeStr = $"{now:yyyyMMddHHmm}";

            var filePath = Path.Combine(Configuration.PluginPath, "GuardCache", timeStr + ".json");

            var sw = new StreamWriter(filePath); 
            await sw.WriteAsync(jsonStr);
            sw.Close();
            Configuration.LatestGuardCache = Path.Combine(Configuration.PluginPath, "GuardCache", timeStr + ".json");
            await UpdateDatabaseAndRole(list);
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

            var message = $"MD-AM - 执行缓存大航海列表完成，总数：{list.Count}/{totalGuards}，" +
                          $"总页数：{totalPages}，成功率：{list.Count / (double) totalGuards * 100}%";
            
            Logger.LogInformation(message);            
            
            await HttpApiRequestService.GetResponse(new CreateMessageRequest()
            {
                ChannelId = Configuration.AdminChannel,
                Content = $"{DateTime.Now} {message}",
                MessageType = 1
            });

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

        private static async Task UpdateDatabaseAndRole(IReadOnlyCollection<Guard> guards)
        {
            using var kaiheilaUserOperation = new KaiheilaUserOperation();
            using var bilibiliUserOperation = new BilibiliUserOperation();
            var list = new List<KaiheilaUser>();
            list.AddRange(kaiheilaUserOperation.GetKaiheilaUserWithRole(Configuration.GovernorRole));
            list.AddRange(kaiheilaUserOperation.GetKaiheilaUserWithRole(Configuration.AdmiralRole));
            list.AddRange(kaiheilaUserOperation.GetKaiheilaUserWithRole(Configuration.CaptainRole));

            var roles = new List<string>()
            {
                Configuration.GovernorRole,
                Configuration.AdmiralRole,
                Configuration.CaptainRole
            };
            
            foreach (var user in list)
            {
                var uid = user.BilibiliUser.Uid;
                var current = guards.FirstOrDefault(x => x.Uid == uid);
                if (current is null)
                {
                    var gl = user.BilibiliUser.GuardLevel;
                    var role = roles[gl - 1];
                    user.BilibiliUser.GuardLevel = 0;
                    await RoleHelper.RevokeRole(user.Uid, role, HttpApiRequestService);
                    var userRoles = user.Roles.Split(' ').ToList();
                    userRoles.Remove(role);
                    user.Roles = string.Join(' ', userRoles);
                    bilibiliUserOperation.UpdateAndSave(user.BilibiliUser);
                    kaiheilaUserOperation.UpdateAndSave(user);
                }
                else if (current.GuardLevel != user.BilibiliUser.GuardLevel)
                {
                    var oldGl = user.BilibiliUser.GuardLevel;
                    var newGl = current.GuardLevel;
                    var oldRole = roles[oldGl - 1];
                    var newRole = roles[newGl - 1];
                    user.BilibiliUser.GuardLevel = current.GuardLevel;
                    await RoleHelper.RevokeRole(user.Uid, oldRole, HttpApiRequestService);
                    await RoleHelper.GrantRole(user.Uid, newRole, HttpApiRequestService);
                    var userRoles = user.Roles.Split(' ').ToList();
                    userRoles.Remove(oldRole);
                    userRoles.Add(newRole);
                    user.Roles = string.Join(' ', userRoles);
                    bilibiliUserOperation.UpdateAndSave(user.BilibiliUser);
                    kaiheilaUserOperation.UpdateAndSave(user);
                }
            }
        }
    }
}
