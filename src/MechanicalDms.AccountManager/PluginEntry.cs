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
            SessionManager.Logger = logger;
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
                                Content = $"(met){e.Data.AuthorId}(met) ??????????????????",
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
                            logger.LogWarning($"MD-AM - ???????????????????????????????????????????????????");
                            api.GetResponse(new CreateMessageRequest()
                            {
                                ChannelId = Configuration.ElementApplyChannel,
                                Content = $"(met){e.Data.AuthorId}(met) ????????????????????????????????????????????????",
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
                                Content = $"(met){e.Data.AuthorId}(met) ???????????? Bilibili ??????",
                                MessageType = 9,
                                Quote = e.Data.MessageId,
                                TempTargetId = e.Data.AuthorId
                            }).Wait();
                            return 1;
                        }

                        var requestElement = args.First();
                        var requestElementRole = requestElement switch
                        {
                            "wind" => Configuration.HerbaElementRole,
                            "aqua" => Configuration.AquaElementRole,
                            "fire" => Configuration.FlameElementRole,
                            "earth" => Configuration.EarthElementRole,
                            _ => null
                        };

                        if (requestElementRole is null)
                        {
                            api.GetResponse(new CreateMessageRequest()
                            {
                                ChannelId = Configuration.ElementApplyChannel,
                                Content = $"(met){e.Data.AuthorId}(met) ????????????",
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
                            Content = $"(met){e.Data.AuthorId}(met) ????????????",
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
                                $"????????????????????????????????????");

                            Scheduler.TriggerJob(new JobKey("fetchBilibiliGuardListJob", "group1")).Wait();

                            api.GetResponse(new CreateMessageRequest()
                            {
                                ChannelId = Configuration.AdminChannel,
                                Content = $"MD-AD - {e.Data.Extra.Author.Username}#" +
                                          $"{e.Data.Extra.Author.IdentifyNumber} ????????????????????????????????????",
                                MessageType = 1
                            }).Wait();

                            return 0;
                        }))
                    .AddChildNode(new CommandNode("disableSession")
                        .SetFunction((_, logger, e, api) =>
                        {
                            logger.LogInformation(
                                $"MD-AM - {e.Data.Extra.Author.Username}#{e.Data.Extra.Author.IdentifyNumber} " +
                                $"???????????????????????? Bilibili ?????? Session ??????");
                            
                            api.GetResponse(new CreateMessageRequest()
                            {
                                ChannelId = Configuration.AdminChannel,
                                Content = $"MD-AD - ???????????????????????? Bilibili ?????? Session ??????",
                                MessageType = 1
                            }).Wait();

                            SessionManager.WaitForFinish(logger).Wait();

                            api.GetResponse(new CreateMessageRequest()
                            {
                                ChannelId = Configuration.AdminChannel,
                                Content = $"MD-AD - ????????????????????? Bilibili ?????? Session ??????",
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
                                $"?????????????????? Bilibili ?????? Session ??????");
                            
                            api.GetResponse(new CreateMessageRequest()
                            {
                                ChannelId = Configuration.AdminChannel,
                                Content = $"MD-AD - ?????????????????? Bilibili ?????? Session ??????",
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
                logger.LogWarning($"MD-AM - {e.Data.Extra.Author.Username} ????????????????????????????????????????????????"); 
                return 1;
            }

            using var operation = new KaiheilaUserOperation(); 
            var user = operation.GetKaiheilaUser(e.Data.AuthorId);
            
            if (user is null) 
            { 
                logger.LogInformation($"MD-AM - ?????? {e.Data.Extra.Author.Username}#{e.Data.Extra.Author.IdentifyNumber} " +
                                      $"???????????????????????????UID???{e.Data.AuthorId}");
                httpApiRequestService.GetResponse(new CreateMessageRequest() 
                {
                    ChannelId = Configuration.BindingChannel, 
                    Content = "?????????????????????????????????????????? `/account bind bili` ??????????????? Bilibili ??????????????????????????????????????????????????????", 
                    MessageType = 9, 
                    Quote = e.Data.MessageId, 
                    TempTargetId = e.Data.AuthorId
                }).Wait(); 
                return 1;
            }

            var mcUuid = "NaN"; 
            var mcPlayerName = "NaN";
            var mcAccount = "NaN";
            
            if (user.MinecraftPlayer is not null) 
            { 
                mcUuid = user.MinecraftPlayer.Uuid; 
                mcPlayerName = user.MinecraftPlayer.PlayerName;
                mcAccount = user.MinecraftPlayer.IsLegitCopy ? "????????????" : "????????????";
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
                        .AddSection(SectionModes.Left, new Kmarkdown($"(met){e.Data.AuthorId}(met) ???????????????????????????"),null)
                        .AddDivider()
                        .AddSection(SectionModes.Left, new Kmarkdown($"Kaiheila UID???{user.Uid}"), null)
                        .AddSection(SectionModes.Left, new Kmarkdown($"Kaiheila ????????????{user.Username}#{user.IdentifyNumber}"), null)
                        .AddDivider()
                        .AddSection(SectionModes.Left, new Kmarkdown($"Bilibili UID???{biliUid}"), null)
                        .AddSection(SectionModes.Left, new Kmarkdown($"Bilibili ????????????{biliName}"), null)
                        .AddSection(SectionModes.Left, new Kmarkdown($"Bilibili ???????????????{biliLevel}"), null)
                        .AddDivider()
                        .AddSection(SectionModes.Left, new Kmarkdown($"Minecraft UUID???{mcUuid}"), null)
                        .AddSection(SectionModes.Left, new Kmarkdown($"Minecraft ????????????{mcPlayerName}"), null)
                        .AddSection(SectionModes.Left, new Kmarkdown($"Minecraft ????????????: {mcAccount}"), null)
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
                logger.LogWarning($"MD-AM - {e.Data.Extra.Author.Username} ???????????? Bilibili ??????????????????????????????");
                httpApiRequestService.GetResponse(new CreateMessageRequest()
                {
                    ChannelId = Configuration.BindingChannel,
                    Content = $"(met){e.Data.AuthorId}(met) ??????????????????????????????",
                    MessageType = 9,
                    Quote = e.Data.MessageId,
                    TempTargetId = e.Data.AuthorId
                }).Wait();
                return 1;
            }
            
            var user = e.Data.Extra.Author;

            var roleStr = string.Join(' ', user.Roles);
            
            if (roleStr.Contains(Configuration.BilibiliBindingRole))
            {
                var t1 = httpApiRequestService.GetResponse(new CreateMessageRequest()
                {
                    ChannelId = Configuration.BindingChannel,
                    Content = $"(met){e.Data.AuthorId}(met) ?????????????????????",
                    MessageType = 9,
                    Quote = e.Data.MessageId,
                    TempTargetId = e.Data.AuthorId
                });
                t1.Wait();
                logger.LogWarning($"MD-AM - {e.Data.Extra.Author.Username} ???????????? Bilibili ?????????????????????????????????");
                if (t1.Result.IsSuccessful is not true)
                {
                    logger.LogWarning($"MD-AM - ?????? \"(met){e.Data.AuthorId}(met) ?????????????????????\" ????????????");
                }
                return 2;
            }
            
            using var khlOperation = new KaiheilaUserOperation();
            khlOperation.AddOrUpdateKaiheilaUser(user.Id, user.Username, user.IdentifyNumber, roleStr);

            var khlId = e.Data.AuthorId;

            var t2 = SessionManager.NewSession(khlId, logger, httpApiRequestService);
            t2.Wait();
            var url = t2.Result;

            string message;
            
            switch (url)
            {
                case null:
                    message = new CardMessageBuilder()
                        .AddCard(new CardBuilder(Themes.Warning, "#66CCFF", Sizes.Lg)
                            .AddModules(new ModuleBuilder()
                                .AddSection(SectionModes.Right, 
                                    new Kmarkdown($@"(met){e.Data.AuthorId}(met) 
Bilibili API ????????????????????????

???????????????????????????????????????????????????"), 
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
?????????????????????????????????
???????????????????????????"), 
                                    null)
                                .Build())
                            .Build())
                        .Build();
                    break;
                default:
                {
                    var kmd = $@"(met){e.Data.AuthorId}(met)
??????????????? Bilibili ??????????????????????????????????????????

???????????????????????? ***120*** ???
?????????????????????????????????????????????";

                    var t3 = QrCodeHelper.GenerateAndUploadQrCode(url, httpApiRequestService);
                    t3.Wait();
                    var qrCode = t3.Result;
                
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

            var status = false;
            var retry = 2;
            while (status is false && retry >= 0)
            {
                var t4 = httpApiRequestService.GetResponse(new CreateMessageRequest() 
                {
                    ChannelId = Configuration.BindingChannel, 
                    MessageType = 10, 
                    Content = message, 
                    Quote = e.Data.MessageId, 
                    TempTargetId = e.Data.AuthorId
                }); 
                t4.Wait();
                Task.Delay(500).Wait();
                status = t4.Result.IsSuccessful;
                retry--;
                if (status is not true)
                {
                    logger.LogWarning($"MD-AM - ?????? {e.Data.AuthorId} ????????????????????????????????????????????? {retry + 1}");
                }
                else
                {
                    logger.LogInformation($"MD-AM - ?????? {e.Data.AuthorId} ?????????????????????");
                }
            }
            
            return 0;
        }
    }
}
