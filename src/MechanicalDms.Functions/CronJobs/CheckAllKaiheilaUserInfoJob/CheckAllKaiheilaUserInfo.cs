using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MechanicalDms.Database;
using MechanicalDms.Database.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RestSharp;

namespace MechanicalDms.Functions.CronJobs.CheckAllKaiheilaUserInfoJob
{
    public static class CheckAllKaiheilaUserInfo
    {
        [Function("CheckAllKaiheilaUserInfo")]
        public static void Run([TimerTrigger("0 0 0/12 * * *")] MyInfo myTimer, FunctionContext context)
        {
            var logger = context.GetLogger("CheckDatabaseFormat");
            logger.LogInformation("开始确认开黑啦用户数据");
            var sr = new StreamReader(Path.Join(Directory.GetCurrentDirectory(), "config.json"));
            var jsonString = sr.ReadToEnd();
            sr.Close();
            var config = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonString);
            if (config is null)
            {
                logger.LogCritical("FATAL ERROR：配置文件读取出错");
                return;
            }
            var restClient = new RestClient(new Uri(config["BaseUrl"]));
            var restRequest = new RestRequest("guild/user-list", Method.GET);
            restRequest.AddHeader("Authorization", $"Bot {config["Token"]}");
            restRequest.AddParameter("guild_id", config["GuildId"]);
            restRequest.AddParameter("page", 1);
            
            var response = restClient.Execute(restRequest);

            if (response.IsSuccessful is not true)
            {
                logger.LogCritical("FATAL ERROR：获取第一页出现错误，获取失败");
                return;
            }

            var dataSection = JsonDocument.Parse(response.Content).RootElement.GetProperty("data");
            var totalPages = dataSection.GetProperty("meta").GetProperty("page_total").GetInt32();
            var totalUsers = dataSection.GetProperty("meta").GetProperty("total").GetInt32();
            
            var allUser = new List<KaiheilaUser>();
            for (var page = 1; page <= totalPages; page++)
            {
                if (page != 1)
                {
                    restRequest.AddOrUpdateParameter("page", page);
                    response = restClient.Execute(restRequest);
                    if (response.IsSuccessful is not true)
                    {
                        logger.LogError($"ERROR：获取第 {page} 页失败");
                        continue;
                    }
                    dataSection = JsonDocument.Parse(response.Content).RootElement.GetProperty("data");
                }
                logger.LogInformation($"获取第 {page} 页成功");

                var guildUsers = dataSection.GetProperty("items").EnumerateArray().ToArray();
                // ReSharper disable once LoopCanBeConvertedToQuery
                foreach (var guildUser in guildUsers)
                {
                    var roles = guildUser.GetProperty("roles").EnumerateArray().ToArray();
                    var roleList = roles.Select(role => role.GetInt32().ToString().Trim()).ToList();
                    allUser.Add(new KaiheilaUser()
                    {
                        Uid = guildUser.GetProperty("id").GetString(),
                        Username = guildUser.GetProperty("username").GetString(),
                        IdentifyNumber = guildUser.GetProperty("identify_num").GetString(),
                        Roles = string.Join(' ', roleList).Trim()
                    });
                }
            }
            using var db = new DmsDbContext();
            var users = db.KaiheilaUsers.ToList();

            var sendMessageParams = new Dictionary<string, string>()
            {
                {"target_id", config["AdminChannel"]},
                {"type", "1"},
                {"content", ""}
            };
            var failed = "";
            var changes = 0;
            foreach (var user in users)
            {
                var currentStatus = allUser.FirstOrDefault(x => x.Uid == user.Uid);
                if (currentStatus is null)
                {
                    failed = failed += user.Uid + "";
                    continue;
                }

                if (user.Username == currentStatus.Username && 
                    user.Roles == currentStatus.Roles &&
                    user.IdentifyNumber == currentStatus.IdentifyNumber)
                {
                    continue;
                }
                
                user.Username = currentStatus.Username;
                user.Roles = currentStatus.Roles;
                user.IdentifyNumber = currentStatus.IdentifyNumber;
                db.KaiheilaUsers.Update(user);
                db.SaveChanges();
                logger.LogInformation($"已修改 {user.Uid}");
                changes++;
            }
            
            if (failed == "")
            {
                failed = "null";
            }
            
            var successRate = allUser.Count / (double) totalUsers * 100;
            var infoRequest = new RestRequest("message/create", Method.POST);
            infoRequest.AddHeader("Authorization", $"Bot {config["Token"]}");
            sendMessageParams["content"] = $"{DateTime.Now} Azure-Functions - 更新开黑啦用户数据完成，修改量：{changes} ，" +
                                           $"从开黑啦 API 获取用户数据成功率：{successRate}% ，" +
                                           $"出错用户：{failed}";
            infoRequest.AddJsonBody(sendMessageParams);
            restClient.Execute(infoRequest);
            logger.LogInformation($"更新开黑啦用户数据完成，修改量：{changes} ，" +
                                  $"从开黑啦 API 获取用户数据成功率：{successRate}% ，" +
                                  $"出错用户：{failed}");
        }
    }

    public class MyInfo
    {
        public MyScheduleStatus ScheduleStatus { get; set; }

        public bool IsPastDue { get; set; }
    }

    public class MyScheduleStatus
    {
        public DateTime Last { get; set; }

        public DateTime Next { get; set; }

        public DateTime LastUpdated { get; set; }
    }
}