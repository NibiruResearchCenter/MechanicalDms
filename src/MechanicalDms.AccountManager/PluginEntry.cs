using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using KaiheilaBot.Core.Common.Builders.CardMessage;
using KaiheilaBot.Core.Common.Serializers;
using KaiheilaBot.Core.Extension;
using KaiheilaBot.Core.Models.Events;
using KaiheilaBot.Core.Models.Events.MessageRelatedEvents;
using KaiheilaBot.Core.Models.Objects.CardMessages.Elements;
using KaiheilaBot.Core.Models.Objects.CardMessages.Enums;
using KaiheilaBot.Core.Models.Requests.ChannelMessage;
using KaiheilaBot.Core.Models.Service;
using KaiheilaBot.Core.Services.IServices;
using MechanicalDms.AccountManager.Binding;
using MechanicalDms.AccountManager.Helpers;
using MechanicalDms.AccountManager.ScheduledJobs;
using MechanicalDms.Operation;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl;

namespace MechanicalDms.AccountManager
{
    public class PluginEntry : IPlugin
    {
        private static IScheduler Scheduler { get; set; }
        
        public async Task Initialize(ILogger<IPlugin> logger, ICommandService commandService, 
            IHttpApiRequestService httpApiRequestService, string pluginPath)
        {
            var config = await YamlSerializer.Deserialize<Dictionary<string, string>>(Path.Combine(pluginPath, "config.yml"));

            #region Map Configuration
            
            Configuration.GuildId = config["GuildId"];
            Configuration.BindingChannel = config["BindingChannel"];
            Configuration.AdminChannel = config["AdminChannel"];
            Configuration.ElementApplyChannel = config["ElementApplyChannel"];
            Configuration.GovernorRole = config["GovernorRole"];
            Configuration.AdmiralRole = config["AdmiralRole"];
            Configuration.CaptainRole = config["CaptainRole"];
            Configuration.BilibiliBindingRole = config["BilibiliBindingRole"];
            Configuration.MinecraftBindingRole = config["MinecraftBindingRole"];
            Configuration.HerbaElementRole = config["HerbaElementRole"];
            Configuration.AquaElementRole = config["AquaElementRole"];
            Configuration.FlameElementRole = config["FlameElementRole"];
            Configuration.EarthElementRole = config["EarthElementRole"];
            Configuration.LiveRoomId = config["LiveRoomId"];
            Configuration.LiveHostId = config["LiveHostId"];
            Configuration.PluginPath = pluginPath;
            
            #endregion

            #region Session Manager Setup

            SessionManager.Api = httpApiRequestService;
            SessionManager.QueryTimer = new Timer(){ Interval = 5 * 1000, Enabled = false, AutoReset = true };
            SessionManager.QueryTimer.Elapsed += SessionManager.QuerySessions;
            SessionManager.QueryTimer.Enabled = true;

            #endregion

            #region Bilibili Guard Cache Setup
            
            FetchBilibiliGuardList.HttpApiRequestService = httpApiRequestService;
            FetchBilibiliGuardList.Logger = logger;

            if (Directory.Exists(Path.Combine(pluginPath, "GuardCache")) is not true)
            {
                Directory.CreateDirectory(Path.Combine(pluginPath, "GuardCache"));
            }
            
            #endregion

            #region Scheduler Setup
            var factory = new StdSchedulerFactory();
            Scheduler = await factory.GetScheduler();

            var fetchBilibiliGuardListJob = JobBuilder.Create<FetchBilibiliGuardList>()
                .WithIdentity(new JobKey("fetchBilibiliGuardListJob", "group1"))
                .Build();
            
            await Scheduler.Start();

            var fetchBilibiliGuardListJobTrigger = TriggerBuilder.Create()
                .WithIdentity("fetchBilibiliGuardListJobTrigger", "group1")
                .WithCronSchedule("0 0 */2 * * ?")
                .StartAt(DateBuilder.EvenHourDate(null))
                .ForJob(new JobKey("fetchBilibiliGuardListJob", "group1"))
                .Build();

            await Scheduler.ScheduleJob(fetchBilibiliGuardListJob, fetchBilibiliGuardListJobTrigger);
            #endregion
            
            #region Latest Cache File Path Setup
            
            var cacheFiles = new DirectoryInfo(Path.Combine(pluginPath, "GuardCache")).GetFiles();

            if (cacheFiles.Length == 0)
            {
                Scheduler.TriggerJob(new JobKey("fetchBilibiliGuardListJob", "group1")).Wait();
            }
            else
            {
                var latestFile = cacheFiles.OrderByDescending(x => x.CreationTime).First();
                Configuration.LatestGuardCache = latestFile.FullName;
            }
            
            #endregion

            #region Prepare QrCode Cache Folder

            if (Directory.Exists(Path.Combine(Configuration.PluginPath, "QrCodeCache")) is not true)
            {
                Directory.CreateDirectory(Path.Combine(Configuration.PluginPath, "QrCodeCache"));
            }
            
            #endregion
            
            AddCommand(commandService);
        }

        public async Task Unload(ILogger<IPlugin> logger, IHttpApiRequestService httpApiRequestService)
        {
            await SessionManager.WaitForFinish(logger);
            await Scheduler.Shutdown();
        }

        private static void AddCommand(ICommandService commandService)
        {
            var accountCommand = new CommandNode("account")
                .AddChildNode(new CommandNode("query")
                    .AddAllowedChannel(Configuration.BindingChannel)
                    .SetFunction((args, logger, e, api) =>
                        QueryCommandFunction(e, api, logger, args)))
                .AddChildNode(new CommandNode("bind")
                    .AddAllowedChannel(Configuration.BindingChannel)
                    .AddChildNode(new CommandNode("bili")
                        .SetFunction((args, logger, e, api) =>
                            BindingBilibiliCommandFunction(e, api, logger, args))))
                .AddChildNode(new CommandNode("element")
                    .AddAllowedChannel(Configuration.ElementApplyChannel)
                    .AddAllowedRoles(Convert.ToInt64(Configuration.BilibiliBindingRole))
                    .SetFunction((args, logger, e, api) =>
                    {
                        if (args.Count != 1)
                        {
                            api.GetResponse(new CreateMessageRequest()
                            {
                                ChannelId = Configuration.ElementApplyChannel,
                                Content = $"(met){e.Data.AuthorId}(met) 参数数量错误",
                                MessageType = 9,
                                Quote = e.Data.MessageId,
                                TempTargetId = e.Data.AuthorId
                            }).Wait();
                            return 2;
                        }

                        var userRoles = e.Data.Extra.Author.Roles.ToList();
                        if (userRoles.Contains(Convert.ToInt64(Configuration.HerbaElementRole)) ||
                            userRoles.Contains(Convert.ToInt64(Configuration.AquaElementRole)) ||
                            userRoles.Contains(Convert.ToInt64(Configuration.FlameElementRole)) ||
                            userRoles.Contains(Convert.ToInt64(Configuration.EarthElementRole)))
                        {
                            logger.LogWarning($"MD-AM - 用户已拥有可申请角色，拒绝新的申请");
                            api.GetResponse(new CreateMessageRequest()
                            {
                                ChannelId = Configuration.ElementApplyChannel,
                                Content = $"(met){e.Data.AuthorId}(met) 您已拥有一个可申请属性，拒绝申请",
                                MessageType = 9,
                                Quote = e.Data.MessageId,
                                TempTargetId = e.Data.AuthorId
                            }).Wait();
                            return 1;
                        }

                        if (userRoles.Contains(Convert.ToInt64(Configuration.BilibiliBindingRole)) is false)
                        {
                            api.GetResponse(new CreateMessageRequest()
                            {
                                ChannelId = Configuration.ElementApplyChannel,
                                Content = $"(met){e.Data.AuthorId}(met) 请先绑定 Bilibili 账号",
                                MessageType = 9,
                                Quote = e.Data.MessageId,
                                TempTargetId = e.Data.AuthorId
                            }).Wait();
                            return 1;
                        }

                        var requestElement = args.First();
                        var requestElementRole = requestElement switch
                        {
                            "herba" => Configuration.HerbaElementRole,
                            "aqua" => Configuration.AquaElementRole,
                            "flame" => Configuration.FlameElementRole,
                            "earth" => Configuration.EarthElementRole,
                            _ => null
                        };

                        if (requestElementRole is null)
                        {
                            api.GetResponse(new CreateMessageRequest()
                            {
                                ChannelId = Configuration.ElementApplyChannel,
                                Content = $"(met){e.Data.AuthorId}(met) 参数错误",
                                MessageType = 9,
                                Quote = e.Data.MessageId,
                                TempTargetId = e.Data.AuthorId
                            }).Wait();
                        }

                        userRoles.Add(Convert.ToInt64(requestElementRole));
                        using var operation = new KaiheilaUserOperation();
                        var user = operation.GetKaiheilaUser(e.Data.AuthorId);
                        user.Roles = string.Join(' ', userRoles);
                        operation.UpdateAndSave(user);
                        RoleHelper.GrantRole(e.Data.AuthorId, requestElementRole, api).Wait();
                        api.GetResponse(new CreateMessageRequest()
                        {
                            ChannelId = Configuration.ElementApplyChannel,
                            Content = $"(met){e.Data.AuthorId}(met) 申请成功",
                            MessageType = 9,
                            Quote = e.Data.MessageId,
                            TempTargetId = e.Data.AuthorId
                        }).Wait();
                        return 0;
                    }))
                .AddChildNode(new CommandNode("run")
                    .AddAllowedChannel(Configuration.AdminChannel)
                    .AddChildNode(new CommandNode("guardCache")
                        .SetFunction((_, logger, e, api) =>
                        {
                            logger.LogInformation(
                                $"MD-AM - {e.Data.Extra.Author.Username}#{e.Data.Extra.Author.IdentifyNumber} " +
                                $"手动执行了缓存大航海列表");

                            Scheduler.TriggerJob(new JobKey("fetchBilibiliGuardListJob", "group1")).Wait();

                            api.GetResponse(new CreateMessageRequest()
                            {
                                ChannelId = Configuration.AdminChannel,
                                Content = $"MD-AD - {e.Data.Extra.Author.Username}#" +
                                          $"{e.Data.Extra.Author.IdentifyNumber} 手动执行了缓存大航海列表",
                                MessageType = 1
                            }).Wait();

                            return 0;
                        }))
                    .AddChildNode(new CommandNode("disableSession")
                        .SetFunction((_, logger, e, api) =>
                        {
                            logger.LogInformation(
                                $"MD-AM - {e.Data.Extra.Author.Username}#{e.Data.Extra.Author.IdentifyNumber} " +
                                $"准备停止接受新的 Bilibili 登录 Session 请求");
                            
                            api.GetResponse(new CreateMessageRequest()
                            {
                                ChannelId = Configuration.AdminChannel,
                                Content = $"MD-AD - 准备停止接受新的 Bilibili 登录 Session 请求",
                                MessageType = 1
                            }).Wait();

                            SessionManager.WaitForFinish(logger).Wait();

                            api.GetResponse(new CreateMessageRequest()
                            {
                                ChannelId = Configuration.AdminChannel,
                                Content = $"MD-AD - 已停止接受新的 Bilibili 登录 Session 请求",
                                MessageType = 1
                            }).Wait();
                            
                            return 0;
                        }))
                    .AddChildNode(new CommandNode("enableSession")
                        .SetFunction((_, logger, e, api) =>
                        {
                            SessionManager.Enable(logger);
                            
                            logger.LogInformation(
                                $"MD-AM - {e.Data.Extra.Author.Username}#{e.Data.Extra.Author.IdentifyNumber} " +
                                $"开始接受新的 Bilibili 登录 Session 请求");
                            
                            api.GetResponse(new CreateMessageRequest()
                            {
                                ChannelId = Configuration.AdminChannel,
                                Content = $"MD-AD - 开始接受新的 Bilibili 登录 Session 请求",
                                MessageType = 1
                            }).Wait();
                            
                            return 0;
                        })));

            commandService.AddCommand(accountCommand);
        }

        private static int QueryCommandFunction(BaseMessageEvent<TextMessageEvent> e,
            IHttpApiRequestService httpApiRequestService,
            ILogger logger, IReadOnlyCollection<string> args)
        {
            if (args.Count != 0)
            { 
                logger.LogWarning($"MD-AM - {e.Data.Extra.Author.Username} 执行查询绑定指令失败，过多的参数"); 
                return 1;
            }

            using var operation = new KaiheilaUserOperation(); 
            var user = operation.GetKaiheilaUser(e.Data.AuthorId);
            
            if (user is null) 
            { 
                logger.LogInformation($"MD-AM - 用户 {e.Data.Extra.Author.Username}#{e.Data.Extra.Author.IdentifyNumber} " +
                                      $"在数据库中不存在，UID：{e.Data.AuthorId}");
                httpApiRequestService.GetResponse(new CreateMessageRequest() 
                {
                    ChannelId = Configuration.BindingChannel, 
                    Content = "查询失败，找不到用户，请输入 `/account bind bili` 来绑定您的 Bilibili 账号，开黑啦用户信息会同时录入数据库", 
                    MessageType = 9, 
                    Quote = e.Data.MessageId, 
                    TempTargetId = e.Data.AuthorId
                }).Wait(); 
                return 1;
            }

            var mcUuid = "NaN"; 
            var mcPlayerName = "NaN";
            
            if (user.MinecraftPlayer is not null) 
            { 
                mcUuid = user.MinecraftPlayer.Uuid; 
                mcPlayerName = user.MinecraftPlayer.PlayerName;
            }

            var biliUid = (long) -1; 
            var biliName = "NaN"; 
            var biliLevel = -1;
            
            if (user.BilibiliUser is not null) 
            { 
                biliUid = user.BilibiliUser.Uid; 
                biliName = user.BilibiliUser.Username;
                biliLevel = user.BilibiliUser.Level;
            }

            var responseCard = new CardMessageBuilder()
                .AddCard(new CardBuilder(Themes.Info, "#66CCFF", Sizes.Lg)
                    .AddModules(new ModuleBuilder()
                        .AddSection(SectionModes.Left, new Kmarkdown($"(met){e.Data.AuthorId}(met) 查询到您的绑定信息"),null)
                        .AddDivider()
                        .AddSection(SectionModes.Left, new Kmarkdown($"Bilibili UID：{biliUid}"), null)
                        .AddSection(SectionModes.Left, new Kmarkdown($"Bilibili Username：{biliName}"), null)
                        .AddSection(SectionModes.Left, new Kmarkdown($"Bilibili Level：{biliLevel}"), null)
                        .AddDivider()
                        .AddSection(SectionModes.Left, new Kmarkdown($"Minecraft UUID：{mcUuid}"), null)
                        .AddSection(SectionModes.Left, new Kmarkdown($"Minecraft Player Name：{mcPlayerName}"), null)
                        .Build())
                    .Build())
                .Build();

            httpApiRequestService.GetResponse(new CreateMessageRequest()
            {
                ChannelId = Configuration.BindingChannel,
                Content = responseCard,
                MessageType = 10,
                Quote = e.Data.MessageId,
                TempTargetId = e.Data.AuthorId
            }).Wait();
                        
            return 0;
        }

        private static int BindingBilibiliCommandFunction(BaseMessageEvent<TextMessageEvent> e,
            IHttpApiRequestService httpApiRequestService,
            ILogger<IPlugin> logger, IReadOnlyCollection<string> args)
        {
            if (args.Count != 0)
            { 
                logger.LogWarning($"MD-AM - {e.Data.Extra.Author.Username} 执行绑定 Bilibili 指令失败，过多的参数");
                httpApiRequestService.GetResponse(new CreateMessageRequest()
                {
                    ChannelId = Configuration.BindingChannel,
                    Content = $"(met){e.Data.AuthorId}(met) 绑定失败，过多的参数",
                    MessageType = 9,
                    Quote = e.Data.MessageId,
                    TempTargetId = e.Data.AuthorId
                }).Wait();
                return 1;
            }
            
            var user = e.Data.Extra.Author;

            var roleStr = string.Join(' ', user.Roles);

            using var khlOperation = new KaiheilaUserOperation();
            
            if (roleStr.Contains(Configuration.BilibiliBindingRole))
            {
                var userInDb = khlOperation.GetKaiheilaUser(user.Id);
                httpApiRequestService.GetResponse(new CreateMessageRequest()
                {
                    ChannelId = Configuration.ElementApplyChannel,
                    Content = $"(met){e.Data.AuthorId}(met) 您已经绑定过了，" +
                              $"Bilibili 用户名：{userInDb.BilibiliUser.Username} Lv.{userInDb.BilibiliUser.Level}",
                    MessageType = 9,
                    Quote = e.Data.MessageId,
                    TempTargetId = e.Data.AuthorId
                }).Wait();
                logger.LogWarning($"MD-AM - {e.Data.Extra.Author.Username} 执行绑定 Bilibili 指令失败，已经有过绑定，" + 
                                  $"Bilibili 用户名：{userInDb.BilibiliUser.Username} Lv.{userInDb.BilibiliUser.Level}");
                return 2;
            }
            
            khlOperation.AddOrUpdateKaiheilaUser(user.Id, user.Username, user.IdentifyNumber, roleStr);

            var khlId = e.Data.AuthorId;

            var t1 = SessionManager.NewSession(khlId, logger, httpApiRequestService);
            t1.Wait();
            var url = t1.Result;

            string message;
            
            switch (url)
            {
                case null:
                    message = new CardMessageBuilder()
                        .AddCard(new CardBuilder(Themes.Warning, "#66CCFF", Sizes.Lg)
                            .AddModules(new ModuleBuilder()
                                .AddSection(SectionModes.Right, 
                                    new Kmarkdown($@"(met){e.Data.AuthorId}(met) 
Bilibili API 请求失败，请重试

若多次重试后仍然出错，请联系管理员"), 
                                    null)
                                .Build())
                            .Build())
                        .Build();
                    break;
                case "close":
                    message = new CardMessageBuilder()
                        .AddCard(new CardBuilder(Themes.Warning, "#66CCFF", Sizes.Lg)
                            .AddModules(new ModuleBuilder()
                                .AddSection(SectionModes.Right, 
                                    new Kmarkdown($@"(met){e.Data.AuthorId}(met) 
机器人拒绝接受新的请求
机器人可能正在关闭"), 
                                    null)
                                .Build())
                            .Build())
                        .Build();
                    break;
                default:
                {
                    var kmd = $@"(met){e.Data.AuthorId}(met)
请使用手机 Bilibili 客户端扫描右侧二维码进行登录

二维码有效时间为 ***120*** 秒
该消息仅您可见，刷新后将会消失";

                    var t2 = QrCodeHelper.GenerateAndUploadQrCode(url, httpApiRequestService);
                    t2.Wait();
                    var qrCode = t2.Result;
                
                    message = new CardMessageBuilder()
                        .AddCard(new CardBuilder(Themes.Info, "#66CCFF", Sizes.Lg)
                            .AddModules(new ModuleBuilder()
                                .AddSection(SectionModes.Right, 
                                    new Kmarkdown(kmd),
                                    new Image(qrCode, "qrcode"))
                                .Build())
                            .Build())
                        .Build();
                    break;
                }
            }

            httpApiRequestService.GetResponse(new CreateMessageRequest()
            {
                ChannelId = e.Data.TargetId,
                MessageType = 10,
                Content = message,
                Quote = e.Data.MessageId,
                TempTargetId = e.Data.AuthorId
            });
            
            return 0;
        }
    }
}
