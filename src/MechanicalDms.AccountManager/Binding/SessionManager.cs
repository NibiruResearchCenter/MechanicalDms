using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;
using KaiheilaBot.Core.Extension;
using KaiheilaBot.Core.Models.Requests.ChannelMessage;
using KaiheilaBot.Core.Services.IServices;
using Microsoft.Extensions.Logging;
using RestSharp;

namespace MechanicalDms.AccountManager.Binding
{
    public static class SessionManager
    {
        private static readonly List<QrCodeLoginSession> Sessions = new();
        private static bool _okForNewSession = true;
        
        public static Timer QueryTimer { get; set; }
        public static IHttpApiRequestService Api {private get; set; }
        public static ILogger<IPlugin> Logger { private get; set; }
        
        public static async Task<string> NewSession(string khlId, ILogger<IPlugin> logger, IHttpApiRequestService api)
        {
            if (_okForNewSession is false)
            {
                return "close";
            }
            var client = new RestClient(new Uri("https://passport.bilibili.com"));
            var request = new RestRequest("qrcode/getLoginUrl", Method.GET);
            request.AddHeader("User-Agent",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.101 Safari/537.36 Edg/91.0.864.48");
            var response = await client.ExecuteAsync(request);
            
            if (response.StatusCode != HttpStatusCode.OK) 
            { 
                logger.LogWarning("MD-AM - 请求 Bilibili API 获取登陆二维码失败"); 
                return null;
            }
            
            var url = JsonDocument.Parse(response.Content).RootElement
                .GetProperty("data").GetProperty("url").GetString();
            var token = JsonDocument.Parse(response.Content).RootElement
                .GetProperty("data").GetProperty("oauthKey").GetString();
                        
            logger.LogDebug($"MD-AM - 用户 {khlId} 请求 Bilibili API 获取登陆二维码，Url = {url}，Token = {token}"); 
            Sessions.Add(new QrCodeLoginSession(token, khlId, api, logger));

            return url;
        }

        public static void QuerySessions(object sender, ElapsedEventArgs e)
        {
            var readyToRemove = new List<QrCodeLoginSession>();
            foreach (var session in Sessions)
            {
                switch (session.Status)
                {
                    case 0:
                        readyToRemove.Add(session);
                        break;
                    case 2:
                        Api.GetResponse(new CreateMessageRequest()
                        {
                            ChannelId = Configuration.BindingChannel,
                            Content = $"(met){session._khlId}(met) 登录超时",
                            MessageType = 9,
                            TempTargetId = session._khlId
                        });
                        readyToRemove.Add(session);
                        break;
                }
            }

            foreach (var session in readyToRemove)
            {
                Logger.LogDebug($"MD-AM - 已移除 {session._khlId} 创建的 Session");
                Sessions.Remove(session);
            }
            Logger.LogDebug($"MD-AM - 剩余 Bilibili 登录 Session 数量 {Sessions.Count}");
        }

        public static async Task WaitForFinish(ILogger<IPlugin> logger)
        {
            _okForNewSession = false;
            while (Sessions.Count != 0)
            {
                logger.LogInformation($"MD-AM - 等待 {Sessions.Count} 个 Bilibili 登录 Session 结束");
                await Task.Delay(5000);
            }
            logger.LogWarning("MD-AM - 所有 Bilibili 登录 Session 已结束");
        }

        public static void Enable(ILogger<IPlugin> logger)
        {
            _okForNewSession = true;
            logger.LogInformation("MD-AM - 开始接受新的 Bilibili 登录 Session 请求");
        }
    }
}