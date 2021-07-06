using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using MechanicalDms.Database;
using MechanicalDms.Database.Models;
using MechanicalDms.Functions.Common;
using MechanicalDms.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestSharp;
using HttpResponse = MechanicalDms.Functions.Models.HttpResponse;

namespace MechanicalDms.Functions.HttpApis
{
    public static class AddMinecraftPlayer
    {
        [Function("AddMinecraftPlayer")]
        public static async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "minecraft-player/add")]
            HttpRequestData req,
            FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger("GetMinecraftPlayer");
            var response = req.CreateResponse();
            response.Headers.Add("Content-Type", "application/json");
            var payload = await req.ReadAsStringAsync();
            if (payload is null)
            {
                logger.LogWarning("请求 Body 为空");
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync(JsonSerializer.Serialize(new HttpResponse()
                {
                    Code = -300,
                    Message = "请求 Body 为空",
                    Data = null
                }));
                return response;
            }
            
            AddMinecraftPlayerRequest body;
            try
            {
                body = JsonSerializer.Deserialize<AddMinecraftPlayerRequest>(payload);
                if (body is null)
                {
                    throw new JsonException("Deserialized result is null");
                }

                if (body.Uuid is null || body.PlayerName is null || body.Uid is null || body.Platform is null)
                {
                    throw new JsonException("Deserialized result parameter is null");
                }

                if (body.Platform != "kaiheila" && body.Platform != "discord")
                {
                    throw new ArgumentException($"Argument value is not valid, Platform = {body.Platform}");
                }
            }
            catch (Exception e)
            {
                logger.LogError($"反序列化请求Body出错，{e.Message}");
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync(JsonSerializer.Serialize(new HttpResponse()
                {
                    Code = -400,
                    Message = "请求 Body 结构不正确",
                    Data = null
                }));
                return response;
            }
            
            await using var db = new DmsDbContext();
            if (db.DiscordUsers.AsNoTracking().FirstOrDefault(x => x.MinecraftPlayer.Uuid == body.Uuid) is not null ||
                db.KaiheilaUsers.AsNoTracking().FirstOrDefault(x => x.MinecraftPlayer.Uuid == body.Uuid) is not null)
            {
                logger.LogError("该 Minecraft 玩家配置已绑定一个用户");
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync(JsonSerializer.Serialize(new HttpResponse()
                {
                    Code = -600,
                    Message = "该 Minecraft 玩家配置已绑定一个用户",
                    Data = null
                }));
                return response;
            }

            // DISCORD
            if (body.Platform == "discord")
            {
                logger.LogInformation("玩家平台: Discord");
                var discordUser = db.DiscordUsers
                    .Include(x=>x.MinecraftPlayer)
                    .FirstOrDefault(x => x.Uid == body.Uid);
                if (discordUser is null)
                {
                    logger.LogError("Unknown Discord User ID");
                    response.StatusCode = HttpStatusCode.BadRequest;
                    await response.WriteStringAsync(JsonSerializer.Serialize(new HttpResponse()
                    {
                        Code = -500,
                        Message = "Unknown Discord User ID",
                        Data = null
                    }));
                    return response;
                }

                if (discordUser.MinecraftPlayer is not null)
                {
                    logger.LogError("This discord user already has a minecraft account connected.");
                    response.StatusCode = HttpStatusCode.BadRequest;
                    await response.WriteStringAsync(JsonSerializer.Serialize(new HttpResponse()
                    {
                        Code = -600,
                        Message = "This discord user already has a minecraft account connected",
                        Data = null
                    }));
                    return response;
                }

                var minecraftPlayer = db.MinecraftPlayers
                    .FirstOrDefault(x => x.Uuid == body.Uuid);
                if (minecraftPlayer is null)
                {
                    logger.LogInformation("Minecraft 玩家配置不存在，创建新的配置");
                    minecraftPlayer = new MinecraftPlayer()
                    {
                        IsLegitCopy = body.IsLegitCopy,
                        PlayerName = body.PlayerName,
                        Uuid = body.Uuid
                    };
                    db.MinecraftPlayers.Add(minecraftPlayer);
                    await db.SaveChangesAsync();
                }

                discordUser.MinecraftPlayer = minecraftPlayer;
                db.DiscordUsers.Update(discordUser);
                await db.SaveChangesAsync();
                
                logger.LogInformation($"请求成功，已添加 Minecraft Player UUID = {body.Uuid}，" +
                                      $"MinecraftPlayerName = {body.PlayerName}，Discord UID = {body.Uid}");
                
                response.StatusCode = HttpStatusCode.OK;
                await response.WriteStringAsync(JsonSerializer.Serialize(new HttpResponse()
                {
                    Code = 0,
                    Message = "请求成功",
                    Data = new Player()
                    {
                        From = "discord",
                        Username = discordUser.Username,
                        IdentifyNumber = discordUser.IdentifyNumber,
                        MinecraftPlayerName = body.PlayerName,
                        MinecraftUuid = body.Uuid,
                        Element = discordUser.Element,
                        IsGuard = discordUser.IsGuard
                    }
                }));
                return response;
            }

            // KAIHEILA
            logger.LogInformation("玩家平台: Kaiheila");
            var kaiheilaUser = db.KaiheilaUsers
                .Include(x => x.MinecraftPlayer)
                .Include(x => x.BilibiliUser)
                .FirstOrDefault(x => x.Uid == body.Uid);
            if (kaiheilaUser is null)
            {
                logger.LogError("不存在的开黑啦用户 ID");
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync(JsonSerializer.Serialize(new HttpResponse()
                {
                    Code = -500,
                    Message = "不存在的开黑啦用户 ID",
                    Data = null
                }));
                return response;
            }
            if (ElementHelper.GetElementFromKaiheila(kaiheilaUser.Roles) == 0)
            {
                logger.LogError("开黑啦用户未设置 Element");
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync(JsonSerializer.Serialize(new HttpResponse()
                {
                    Code = -500,
                    Message = "开黑啦用户未设置 Element",
                    Data = null
                }));
                return response;
            }
            if (kaiheilaUser.BilibiliUser is null)
            {
                logger.LogError("开黑啦用户未绑定 Bilibili 账号");
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync(JsonSerializer.Serialize(new HttpResponse()
                {
                    Code = -500,
                    Message = "开黑啦用户未绑定 Bilibili 账号",
                    Data = null
                }));
                return response;
            }
            if (kaiheilaUser.MinecraftPlayer is not null)
            {
                logger.LogError("开黑啦用户已绑定 Minecraft 账号");
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync(JsonSerializer.Serialize(new HttpResponse()
                {
                    Code = -600,
                    Message = "开黑啦用户已绑定 Minecraft 账号",
                    Data = null
                }));
                return response;
            }
            
            // 读取配置文件
            var sr = new StreamReader(Path.Join(Directory.GetCurrentDirectory(), "config.json"));
            var jsonString = await sr.ReadToEndAsync();
            sr.Close();
            var config = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonString);
            if (config is null)
            {
                logger.LogError("配置文件读取错误");
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync(JsonSerializer.Serialize(new HttpResponse()
                {
                    Code = -700,
                    Message = "配置文件读取错误",
                    Data = null
                }));
                return response;
            }

            // 请求开黑啦 API 添加 Role
            var restClient = new RestClient(new Uri(config["BaseUrl"]));
            var restRequest = new RestRequest("guild-role/grant", Method.POST);
            restRequest.AddHeader("Authorization", $"Bot {config["Token"]}");
            restRequest.AddJsonBody(new Dictionary<string, string>()
            {
                {"guild_id", config["GuildId"]},
                {"user_id", body.Uid},
                {"role_id", config["MinecraftBindingRole"]}
            });
            var restResponse = await restClient.ExecuteAsync(restRequest);
            logger.LogInformation("请求开黑啦API完成");
            if (restResponse.IsSuccessful is not true)
            {
                logger.LogError($"开黑啦API请求失败，Kaiheila UID = {body.Uid}，" +
                                $"Status = {restResponse.StatusCode}" +
                                $"Body = {restResponse.Content}");
            }
            
            // 添加至数据库
            var mcPlayer = db.MinecraftPlayers
                .FirstOrDefault(x => x.Uuid == body.Uuid);
            if (mcPlayer is null)
            {
                logger.LogInformation("Minecraft 玩家配置不存在，创建新的配置");
                mcPlayer = new MinecraftPlayer()
                {
                    IsLegitCopy = body.IsLegitCopy,
                    PlayerName = body.PlayerName,
                    Uuid = body.Uuid
                };
                db.MinecraftPlayers.Add(mcPlayer);
                await db.SaveChangesAsync();
            }

            kaiheilaUser.MinecraftPlayer = mcPlayer;
            var roles = kaiheilaUser.Roles.Trim().Split(' ').ToList();
            roles = roles.Select(x => x.Trim()).Where(x => x != string.Empty).ToList();
            roles.Add(config["MinecraftBindingRole"].Trim());
            kaiheilaUser.Roles = string.Join(' ', roles);
            db.KaiheilaUsers.Update(kaiheilaUser);
            await db.SaveChangesAsync();
            
            logger.LogInformation($"请求成功，已添加 Minecraft Player UUID = {body.Uuid}，" +
                                  $"MinecraftPlayerName = {body.PlayerName}，开黑啦 UID = {body.Uid}");
            
            response.StatusCode = HttpStatusCode.OK;
            await response.WriteStringAsync(JsonSerializer.Serialize(new HttpResponse()
            {
                Code = 0,
                Message = "请求成功",
                Data = new Player()
                {
                    From = "kaiheila",
                    Username = kaiheilaUser.Username,
                    IdentifyNumber = kaiheilaUser.IdentifyNumber,
                    MinecraftPlayerName = body.PlayerName,
                    MinecraftUuid = body.Uuid,
                    Element = ElementHelper.GetElementFromKaiheila(kaiheilaUser.Roles),
                    IsGuard = ElementHelper.IsGuardFromKaiheila(kaiheilaUser.Roles)
                }
            }));
            return response;
        }
    }
}
