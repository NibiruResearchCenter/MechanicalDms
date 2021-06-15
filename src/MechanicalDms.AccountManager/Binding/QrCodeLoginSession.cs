using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Timers;
using System.Web;
using KaiheilaBot.Core.Extension;
using KaiheilaBot.Core.Models.Requests.ChannelMessage;
using KaiheilaBot.Core.Services.IServices;
using MechanicalDms.AccountManager.Models;
using MechanicalDms.Database.Models;
using MechanicalDms.Operation;
using Microsoft.Extensions.Logging;
using RestSharp;
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo

namespace MechanicalDms.AccountManager.Binding
{
    public class QrCodeLoginSession
    {
        private readonly string _token;
        private readonly Timer _checkLoginTimer;
        private readonly Timer _timeoutTimer;
        private readonly RestClient _restClient;
        private readonly RestRequest _restRequest;
        private readonly IHttpApiRequestService _api;
        private readonly ILogger<IPlugin> _logger;
        
        public readonly string _khlId;

        public int Status { get; private set; } = 1;

        public QrCodeLoginSession(string token, string khlId, IHttpApiRequestService api, ILogger<IPlugin> logger)
        {
            _logger = logger;
            _token = token;
            _khlId = khlId;
            _api = api;
            _restClient = new RestClient(new Uri("https://passport.bilibili.com"));
            _restRequest = new RestRequest("qrcode/getLoginInfo", Method.POST);
            _restRequest.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            _restRequest.AddParameter("oauthKey", _token);
            _timeoutTimer = new Timer() { AutoReset = false, Interval = 121 * 1000, Enabled = false };
            _timeoutTimer.Elapsed += TimeoutSession;
            _checkLoginTimer = new Timer() { AutoReset = true, Interval = 3000, Enabled = false };
            _checkLoginTimer.Elapsed += CheckLogin;
            _timeoutTimer.Enabled = true;
            _checkLoginTimer.Enabled = true;
        }

        private void TimeoutSession(object sender, ElapsedEventArgs e)
        {
            Status = 2;
            _logger.LogWarning($"MD-AM - Bilibili 登录 Session 超时，UID = {_khlId}，Token = {_token}");
            _checkLoginTimer.Enabled = false;
            _timeoutTimer.Enabled = false;
        }

        private void CheckLogin(object sender, ElapsedEventArgs e)
        {
            var response = _restClient.Execute(_restRequest);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                _logger.LogDebug("MD-AM - 向 Bilibili API 请求登陆状态，请求失败");
                return;
            }

            _logger.LogDebug($"MD-AM - 向 Bilibili API 请求登陆状态，请求成功，获取内容：{response.Content}");
            var json = JsonDocument.Parse(response.Content).RootElement;
            if (json.GetProperty("status").GetBoolean() is not true)
            {
                return;
            }

            _checkLoginTimer.Close();
            var url = json.GetProperty("data").GetProperty("url").GetString();
            var uri = new Uri(url!);
            var qs = HttpUtility.ParseQueryString(uri.Query);
            _logger.LogDebug($"MD-AM - 用户 {_khlId} 登陆 Bilibili 账号成功，Token = {_token}，SESSDATA = {qs["SESSDATA"]}");
            Binding(qs["DedeUserID"], qs["DedeUserID__ckMd5"], qs["SESSDATA"], qs["bili_jct"]);
            Status = 0;
            _checkLoginTimer.Dispose();
        }

        private void Binding(string DedeUserID, string DedeUserID__ckMd5, string SESSDATA, string bili_jct)
        {
            var user = GetUserData(DedeUserID, DedeUserID__ckMd5, SESSDATA, bili_jct);
            using var biliOperation = new BilibiliUserOperation();
            using var khlOperation = new KaiheilaUserOperation();
            _logger.LogDebug($"MD-AM - 获取到 用户 {_khlId} 的 Bilibili 账户信息：" +
                             $"{user.Uid} {user.Username} Lv.{user.Level}，大航海等级：{user.GuardLevel}");
            biliOperation.AddOrUpdateBilibiliUser(user.Uid, user.Username, user.GuardLevel, user.Level);
            khlOperation.BindingBilibili(_khlId, user.Uid);
        }

        private BilibiliUser GetUserData(string DedeUserID, string DedeUserID__ckMd5, string SESSDATA, string bili_jct)
        {
            var cookie =
                $"DedeUserID={DedeUserID};DedeUserID__ckMd5={DedeUserID__ckMd5};SESSDATA={SESSDATA};bili_jct={bili_jct};";
            var client = new RestClient(new Uri("https://api.bilibili.com"));
            var accRequest = new RestRequest("x/member/web/account", Method.GET);
            accRequest.AddHeader("Cookie", cookie);

            var accResponse = client.Execute(accRequest);

            if (accResponse.StatusCode != HttpStatusCode.OK)
            {
                _logger.LogWarning($"MD-AM - 获取用户 {_khlId} Bilibili 账号信息失败，SESSDATA = {SESSDATA}");
                return null;
            }

            if (JsonDocument.Parse(accResponse.Content).RootElement.GetProperty("code").GetInt32() != 0)
            {
                _logger.LogWarning($"MD-AM - 获取用户 {_khlId} Bilibili 账号信息失败，SESSDATA = {SESSDATA}");
                return null;
            }
            _logger.LogDebug($"MD-AM - 获取 Bilibili 用户账号信息：{accResponse.Content}");

            var mid = JsonDocument.Parse(accResponse.Content).RootElement
                .GetProperty("data").GetProperty("mid").GetInt64();

            var userRequest = new RestRequest("x/space/acc/info", Method.GET);
            userRequest.AddParameter("jsonp", "jsonp");
            userRequest.AddParameter("mid", mid);

            var userResponse = client.Execute(userRequest);

            if (userResponse.StatusCode != HttpStatusCode.OK)
            {
                _logger.LogWarning($"MD-AM - 获取用户 {_khlId} Bilibili 账号信息失败，SESSDATA = {SESSDATA}，MID = {mid}");
                return null;
            }
            _logger.LogDebug($"MD-AM - 获取 Bilibili 用户信息：{userResponse.Content}");

            var jsonData = JsonDocument.Parse(userResponse.Content).RootElement.GetProperty("data");

            var uid = jsonData.GetProperty("mid").GetInt64();
            var username = jsonData.GetProperty("name").GetString();
            var level = jsonData.GetProperty("level").GetInt32();
            var guardLevel = 0;
            
            _logger.LogDebug($"MD-AM - 获取到 用户 {_khlId} 的 Bilibili 账户信息：" +
                             $"{uid} {username} Lv.{level}");

            var sr = new StreamReader(Configuration.LatestGuardCache);
            var cacheJson = sr.ReadToEnd();
            var cacheData = JsonSerializer.Deserialize<List<Guard>>(cacheJson);
            sr.Close();
            
            _logger.LogDebug("MD-AM - 已读取大航海数据缓存文件");

            var cacheUser = cacheData!.FirstOrDefault(x => x.Uid == uid);
            
            if (cacheUser is not null)
            {
                _logger.LogDebug($"MD-AM - 成功获取 Bilibili 用户 {uid} {username} 的大航海等级：{cacheUser.GuardLevel}");
                guardLevel = cacheUser.GuardLevel;
            }

            _logger.LogDebug($"MD-AM - 获取到 用户 {_khlId} 的 Bilibili 账户信息：" +
                             $"{uid} {username} Lv.{level}，大航海等级：{guardLevel}");
            
            _api.SetResourcePath("guild-role/grant")
                .SetMethod(Method.POST)
                .AddPostBody("guild_id", Configuration.GuildId)
                .AddPostBody("user_id", _khlId)
                .AddPostBody("role_id", Convert.ToUInt32(Configuration.BilibiliBindingRole))
                .GetResponse().Wait();
            
            _logger.LogDebug($"MD-AM - 已授予用户 {_khlId}，Bilibili {uid} {username} 角色：已绑定 Bilibili {Configuration.BilibiliBindingRole}");

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
                .GetResponse().Wait();
            
            _logger.LogDebug($"MD-AM - 已授予用户 {_khlId}，Bilibili {uid} {username} 角色：{guardLevel} - {guardRole}");

            _api.GetResponse(new CreateMessageRequest()
            {
                ChannelId = Configuration.QueryChannel,
                Content = $"(met){_khlId}(met) 您已绑定 Bilibili 账号 {uid} {username} Lv.{level}，你可以输入 /account query 查询绑定状态",
                MessageType = 9,
                TempTargetId = _khlId
            }).Wait();

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
