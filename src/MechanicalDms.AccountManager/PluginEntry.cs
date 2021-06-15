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
            Configuration.QueryChannel = config["QueryChannel"];
            Configuration.AdminChannel = config["AdminChannel"];
            Configuration.GovernorRole = config["GovernorRole"];
            Configuration.AdmiralRole = config["AdmiralRole"];
            Configuration.CaptainRole = config["CaptainRole"];
            Configuration.BilibiliBindingRole = config["BilibiliBindingRole"];
            Configuration.MinecraftBindingRole = config["MinecraftBindingRole"];
            Configuration.LiveRoomId = config["LiveRoomId"];
            Configuration.LiveHostId = config["LiveHostId"];
            Configuration.PluginPath = pluginPath;
            
            #endregion

            #region Session Manager Setup

            SessionManager.Api = httpApiRequestService;
            SessionManager.QueryTimer = new Timer(){ Interval = 3 * 1000, Enabled = false, AutoReset = true };
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
            await Scheduler.Shutdown();
        }

        private static void AddCommand(ICommandService commandService)
        {
            var accountCommand = new CommandNode("account")
                .AddChildNode(new CommandNode("query")
                    .AddAllowedChannel(Configuration.QueryChannel)
                    .SetFunction((args, logger, e, api) => 
                        QueryCommandFunction(e, api, logger, args)))
                .AddChildNode(new CommandNode("bind")
                    .AddAllowedChannel(Configuration.QueryChannel)
                    .AddChildNode(new CommandNode("bili")
                        .SetFunction((args, logger, e, api) => 
                            BindingBilibiliCommandFunction(e, api, logger, args))))
                .AddChildNode(new CommandNode("run")
                    .AddAllowedChannel(Configuration.AdminChannel)
                    .AddChildNode(new CommandNode("guardCache")
                        .SetFunction((_, logger, e, api) =>
                        {
                            logger.LogInformation($"MD-AD - {e.Data.Extra.Author.Username}#{e.Data.Extra.Author.IdentifyNumber} " +
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
                var t1 = httpApiRequestService.GetResponse(new CreateMessageRequest() 
                {
                    ChannelId = Configuration.QueryChannel, 
                    Content = "查询失败，找不到用户，请输入 `/account getId` 获取您的 ID，数据会自动录入数据库。该 ID 可在绑定 Bilibili 账号时使用", 
                    MessageType = 9, 
                    Quote = e.Data.MessageId, 
                    TempTargetId = e.Data.AuthorId
                }); 
                t1.Wait(); 
                logger.LogDebug($"MD-AM - HttpApi 请求 Response：{t1.Result.Content}"); 
                return 0;
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
                logger.LogCritical("到这里了！！！");
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

            var t2 = httpApiRequestService.GetResponse(new CreateMessageRequest()
            {
                ChannelId = Configuration.QueryChannel,
                Content = responseCard,
                MessageType = 10,
                Quote = e.Data.MessageId,
                TempTargetId = e.Data.AuthorId
            }); 
            t2.Wait();
            logger.LogDebug($"MD-AM - HttpApi 请求 Response：{t2.Result.Content}");
                        
            return 0;
        }

        private static int BindingBilibiliCommandFunction(BaseMessageEvent<TextMessageEvent> e,
            IHttpApiRequestService httpApiRequestService,
            ILogger<IPlugin> logger, IReadOnlyCollection<string> args)
        {
            if (args.Count != 0)
            { 
                logger.LogWarning($"MD-AM - {e.Data.Extra.Author.Username} 执行绑定 Bilibili 指令失败，过多的参数"); 
                return 1;
            }
            
            var user = e.Data.Extra.Author;

            var roleStr = string.Join(' ', user.Roles);
                        
            using var khlOperation = new KaiheilaUserOperation();
            khlOperation.AddOrUpdateKaiheilaUser(user.Id, user.Username, user.IdentifyNumber, roleStr);

            var khlId = e.Data.AuthorId;

            var t1 = SessionManager.NewSession(khlId, logger, httpApiRequestService);
            t1.Wait();
            var url = t1.Result;

            string message;
            
            if (url is null)
            {
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
            }
            else
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
