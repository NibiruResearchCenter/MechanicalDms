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
        
        public static Timer QueryTimer { get; set; }
        public static IHttpApiRequestService Api {private get; set; }
        
        public static async Task<string> NewSession(string khlId, ILogger<IPlugin> logger, IHttpApiRequestService api)
        {
            var client = new RestClient(new Uri("https://passport.bilibili.com"));
            var request = new RestRequest("qrcode/getLoginUrl", Method.GET); 
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
                        Api.GetResponse(new CreateMessageRequest()
                        {
                            ChannelId = Configuration.QueryChannel,
                            Content = $"(met){session._khlId}(met) 您已成功绑定 Bilibili 账号",
                            MessageType = 9,
                            TempTargetId = session._khlId
                        });
                        readyToRemove.Add(session);
                        break;
                    case 2:
                        Api.GetResponse(new CreateMessageRequest()
                        {
                            ChannelId = Configuration.QueryChannel,
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
                Sessions.Remove(session);
            }
        }
    }
}