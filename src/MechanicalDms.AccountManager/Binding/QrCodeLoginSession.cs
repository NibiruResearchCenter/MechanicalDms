using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Timers;
using System.Web;
using KaiheilaBot.Core.Services.IServices;
using MechanicalDms.AccountManager.Models;
using MechanicalDms.Database.Models;
using MechanicalDms.Operation;
using RestSharp;

namespace MechanicalDms.AccountManager.Binding
{
    public class QrCodeLoginSession
    {
        private readonly string _token;
        private readonly string _khlId;
        private readonly Timer _checkLoginTimer;
        private readonly RestClient _restClient;
        private readonly RestRequest _restRequest;
        private readonly IHttpApiRequestService _api;

        public bool IsSuccess { get; set; } = false;
        public string Url { get; set; }
        
        public QrCodeLoginSession(string token, string khlId, string url, IHttpApiRequestService api)
        {
            Url = url;
            _token = token;
            _khlId = khlId;
            _api = api;
            _restClient = new RestClient(new Uri("https://passport.bilibili.com"));
            _restRequest = new RestRequest("qrcode/getLoginInfo", Method.POST);
            _restRequest.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            _restRequest.AddJsonBody("oauthKey", _token);
            _checkLoginTimer = new Timer() { AutoReset = true, Interval = 2000, Enabled = false };
            _checkLoginTimer.Elapsed += CheckLogin;
        }

        private void CheckLogin(object sender, ElapsedEventArgs e)
        {
            var response = _restClient.Execute(_restRequest);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                return;
            }

            var json = JsonDocument.Parse(response.Content).RootElement;
            _checkLoginTimer.Close();
            if (json.GetProperty("status").GetBoolean() is true)
            {
                var url = json.GetProperty("data").GetProperty("url").GetString();
                var uri = new Uri(url!);
                var qs = HttpUtility.ParseQueryString(uri.Query);
                Binding(qs["DedeUserID"], qs["DedeUserID__ckMd5"], qs["SESSDATA"], qs["bili_jct"]);
            }
            _checkLoginTimer.Dispose();
        }

        private void Binding(string DedeUserID, string DedeUserID__ckMd5, string SESSDATA, string bili_jct)
        {
            var user = GetUserData(SESSDATA);
            using var biliOperation = new BilibiliUserOperation();
            using var khlOperation = new KaiheilaUserOperation();
            biliOperation.AddOrUpdateBilibiliUser(user.Uid, user.Username, user.GuardLevel, user.Level);
            khlOperation.BindingBilibili(_khlId, user.Uid);
        }

        private BilibiliUser GetUserData(string SESSDATA)
        {
            var client = new RestClient(new Uri("https://api.bilibili.com"));
            var accRequest = new RestRequest("x/member/web/account", Method.GET);
            accRequest.AddParameter("SESSDATA", SESSDATA);

            var accResponse = client.Execute(accRequest);

            if (accResponse.StatusCode != HttpStatusCode.OK)
            {
                return null;
            }

            var mid = JsonDocument.Parse(accResponse.Content).RootElement
                .GetProperty("data").GetProperty("mid").GetInt64();

            var userRequest = new RestRequest("x/space/acc/info", Method.GET);
            userRequest.AddParameter("jsonp", "jsonp");
            userRequest.AddParameter("mid", mid);

            var userResponse = client.Execute(userRequest);

            if (userResponse.StatusCode != HttpStatusCode.OK)
            {
                return null;
            }

            var jsonData = JsonDocument.Parse(userResponse.Content).RootElement.GetProperty("data");

            var uid = jsonData.GetProperty("mid").GetInt64();
            var username = jsonData.GetProperty("name").GetString();
            var level = jsonData.GetProperty("level").GetInt32();
            var guardLevel = 0;

            var sr = new StreamReader(Configuration.LatestGuardCache);
            var cacheData = JsonSerializer.Deserialize<List<Guard>>(sr.ReadToEnd());
            sr.Close();

            var cacheUser = cacheData!.FirstOrDefault(x => x.Uid == uid);

            if (cacheUser is not null)
            {
                guardLevel = cacheUser.GuardLevel;
            }

            _api.SetResourcePath("guild-role/grant")
                .SetMethod(Method.POST)
                .AddPostBody("guild_id", Configuration.GuildId)
                .AddPostBody("user_id", _khlId)
                .AddPostBody("role_id", Convert.ToUInt32(Configuration.BilibiliBindingRole))
                .GetResponse();

            if (guardLevel == 0)
            {
                return new BilibiliUser() 
                {
                    Uid = uid,
                    Username = username,
                    Level = level,
                    GuardLevel = guardLevel
                };
            }
            
            var guardRole = guardLevel switch
            {
                1 => Configuration.GovernorRole,
                2 => Configuration.AdmiralRole,
                3 => Configuration.CaptainRole,
                _ => null
            };
            
            _api.SetResourcePath("guild-role/grant")
                .SetMethod(Method.POST)
                .AddPostBody("guild_id", Configuration.GuildId)
                .AddPostBody("user_id", _khlId)
                .AddPostBody("role_id", Convert.ToUInt32(guardRole))
                .GetResponse();

            return new BilibiliUser()
            {
                Uid = uid,
                Username = username,
                Level = level,
                GuardLevel = guardLevel
            };
        }
    }
}
