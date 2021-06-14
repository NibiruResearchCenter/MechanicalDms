using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using KaiheilaBot.Core.Extension;
using KaiheilaBot.Core.Services.IServices;
using MechanicalDms.AccountManager.Binding;
using Microsoft.Extensions.Logging;
using RestSharp;

namespace MechanicalDms.AccountManager.Executors
{
    public class HttpDataExecutor : IHttpServerDataResolver
    {
        private static List<QrCodeLoginSession> _sessions = new();

        public async Task<string> Resolve(string data, ILogger<IPlugin> logger, IHttpApiRequestService httpApiRequestService)
        {
            var json = JsonDocument.Parse(data).RootElement;
            var mode = json.GetProperty("mode").GetString();
            switch (mode)
            {
                case "qrCodeLogin":
                    var khlId = json.GetProperty("kaiheila_id").GetString();
                    if (khlId is null)
                    {
                        return "{\"mode\":\"qrCodeLogin\", \"url\":\"null\"}";
                    }
                    var url = await NewSession(khlId, httpApiRequestService);
                    return "{\"mode\":\"qrCodeLogin\", \"url\":\"" + url + "\"}";
                case "qrCodeLoginQuery":
                    var queryUrl = json.GetProperty("url").GetString();
                    if (queryUrl is null)
                    {
                        return "{\"mode\":\"qrCodeLoginQuery\", \"status\":\"null\"}";
                    }

                    var session =
                        _sessions.FirstOrDefault(x => x.Url == queryUrl);
                    var status = session?.IsSuccess;
                    if (status is null)
                    {
                        return "{\"mode\":\"qrCodeLoginQuery\", \"status\":\"null\"}";
                    }
                    if (status == true)
                    {
                        _sessions.Remove(session);
                    }
                    return "{\"mode\":\"qrCodeLoginQuery\", \"status\":\""+ status +"\"}";
                case "query":
                    break;
            }
            return "{\"mode\":\"null\"}";
        }

        private static async Task<string> NewSession(string khlId, IHttpApiRequestService api)
        {
            var client = new RestClient(new Uri("https://passport.bilibili.com"));
            var request = new RestRequest("qrcode/getLoginUrl", Method.GET);
            var response = await client.ExecuteAsync(request);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                return null;
            }

            var url = JsonDocument.Parse(response.Content).RootElement
                .GetProperty("data").GetProperty("url").GetString();
            var token = JsonDocument.Parse(response.Content).RootElement
                .GetProperty("data").GetProperty("oauthKey").GetString();
            
            _sessions.Add(new QrCodeLoginSession(token, khlId, url, api));

            return url;
        }
    }
}
