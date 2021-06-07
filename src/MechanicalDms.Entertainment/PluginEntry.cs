using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using KaiheilaBot.Core.Common.Builders.CardMessage;
using KaiheilaBot.Core.Extension;
using KaiheilaBot.Core.Models.Objects.CardMessages.Elements;
using KaiheilaBot.Core.Models.Objects.CardMessages.Enums;
using KaiheilaBot.Core.Models.Requests.ChannelMessage;
using KaiheilaBot.Core.Models.Service;
using KaiheilaBot.Core.Services.IServices;
using Microsoft.Extensions.Logging;

namespace MechanicalDms.Entertainment
{
    public class PluginEntry : IPlugin
    {
        public async Task Initialize(ILogger<IPlugin> logger, ICommandService commandService, 
            IHttpApiRequestService httpApiRequestService, string pluginPath)
        {
            var resourcePath = Path.Combine(pluginPath, "Resources");
            var diceResourcePath = Path.Combine(resourcePath, "Dice");
            
            logger.LogDebug($"MD-ET - 骰子图像资源路径：{diceResourcePath}");

            for (var i = 1; i <= 6; i++)
            {
                var response = await httpApiRequestService
                    .SetFileUpload(Path.Combine(diceResourcePath, $"{i}.png"))
                    .GetResponse();

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new HttpRequestException("Entertainment 插件上传资源失败");
                }

                var url = JsonDocument.Parse(response.Content).RootElement
                    .GetProperty("data").GetProperty("url").GetString();
                
                if (url is null)
                {
                    throw new HttpRequestException("Entertainment 插件上传资源失败");
                }
                
                logger.LogDebug($"MD-ET - 已创建骰子点数为 {i} 的素材，URL：{url}");
                ResourceUrls.DiceResource.Add(i, url);
            }
            
            await AddCommand(commandService);
        }

        public Task Unload(ILogger<IPlugin> logger, IHttpApiRequestService httpApiRequestService)
        { 
            return Task.CompletedTask;
        }

        private static Task AddCommand(ICommandService commandService)
        {
            var diceCommand = new CommandNode("dice")
                .SetFunction((args, log, e, api) =>
                {
                    if (args.Count >= 2)
                    {
                        log.LogError($"MD-ET - {e.Data.Extra.Author.Username} 掷骰子失败，参数错误");
                        return 1;
                    }

                    var times = 1;
                    if (args.Count == 1)
                    {
                        try
                        {
                            times = Convert.ToInt32(args.First());
                        }
                        catch (Exception ex)
                        {
                            log.LogError($"MD-ET - {e.Data.Extra.Author.Username} 掷骰子失败，" +
                                         $"参数错误：{args.First()}，" +
                                         $"错误：{ex.Message}");
                            return 2;
                        }
                    }

                    if (times is > 9 or <= 0)
                    {
                        return 3;
                    }

                    var result = new List<int>();
                    var rand = new Random();
                    for (var i = 1; i <= times; i++)
                    {
                        result.Add(rand.Next(1, 6));
                    }

                    log.LogDebug($"MD-ET - {e.Data.Extra.Author.Username} 掷骰子掷出了 {result.Sum()} 点");

                    var context = new ContextElementBuilder()
                        .AddElement(new Kmarkdown($"(met){e.Data.AuthorId}(met) 掷出了 {result.Sum()} 点！"));

                    context = result.Aggregate(context, (current, dice) => 
                        current.AddElement(new Image(ResourceUrls.DiceResource[dice])));
                    
                    var returnMessage = new CardMessageBuilder()
                        .AddCard(new CardBuilder(Themes.Primary, "#303030", Sizes.Lg)
                            .AddModules(new ModuleBuilder().AddContext(context.Build()).Build())
                            .Build())
                        .Build();
                        
                    api.GetResponse(new CreateMessageRequest()
                    {
                        MessageType = 10,
                        Content = returnMessage,
                        ChannelId = e.Data.TargetId,
                        Quote = e.Data.MessageId
                    }).Wait();
                    
                    log.LogInformation($"MD-ET - {e.Data.Extra.Author.Username} 掷骰子掷出了 {result.Sum()} 点，已成功发送了消息");
                    
                    return 0;
                });
            
            commandService.AddCommand(diceCommand);
            return Task.CompletedTask;
        }
    }
}
