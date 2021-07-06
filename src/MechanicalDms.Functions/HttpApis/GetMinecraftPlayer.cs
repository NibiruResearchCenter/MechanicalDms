using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using MechanicalDms.Database;
using MechanicalDms.Functions.Common;
using MechanicalDms.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicalDms.Functions.HttpApis
{
    public static class GetMinecraftPlayer
    {
        [Function("GetMinecraftPlayer")]
        public static async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get",Route = "minecraft-player/get")]
            HttpRequestData req,
            FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger("GetMinecraftPlayer");
            logger.LogInformation($"收到请求 URL = {req.Url}");

            var qString = HttpUtility.ParseQueryString(req.Url.Query);
            var response = req.CreateResponse();
            response.Headers.Add("Content-Type", "application/json");

            if (qString.AllKeys.Contains("uuid") is not true)
            {
                logger.LogWarning("不存在 UUID 检索字符串");
                response.StatusCode = HttpStatusCode.NotFound;
                await response.WriteStringAsync(JsonSerializer.Serialize(new HttpResponse()
                {
                    Code = -100,
                    Message = "不存在 UUID 检索字符串",
                    Data = null
                }));
                return response;
            }

            await using var db = new DmsDbContext();
            var kaiheilaPlayer = db.KaiheilaUsers
                .AsNoTracking()
                .Include(x => x.MinecraftPlayer)
                .FirstOrDefault(x => x.MinecraftPlayer.Uuid == qString["uuid"]);
            
            var discordPlayer = db.DiscordUser
                .AsNoTracking()
                .Include(x => x.MinecraftPlayer)
                .FirstOrDefault(x => x.MinecraftPlayer.Uuid == qString["uuid"]);
            
            var player = new Player();

            // 找不到用户
            // ReSharper disable once ConvertIfStatementToSwitchStatement
            if (kaiheilaPlayer is null && discordPlayer is null)
            {
                logger.LogInformation($"找不到玩家 UUID = {qString["uuid"]}");
                response.StatusCode = HttpStatusCode.NotFound;
                await response.WriteStringAsync(JsonSerializer.Serialize(new HttpResponse()
                {
                    Code = -200,
                    Message = "不存在的 Minecraft Player",
                    Data = null
                }));
                logger.LogWarning($"玩家 UUID = {qString["uuid"]} 不存在");
                return response;
            }
            
            // Discord 用户
            if (kaiheilaPlayer is null)
            {
                logger.LogInformation("找到玩家属于 Discord 平台");
                if (qString.AllKeys.Contains("player_name") is true)
                {
                    var oldName = discordPlayer.MinecraftPlayer.PlayerName;
                    if (qString["player_name"] != discordPlayer.MinecraftPlayer.PlayerName)
                    {
                        discordPlayer.MinecraftPlayer.PlayerName = qString["player_name"];
                        db.DiscordUser.Update(discordPlayer);
                        await db.SaveChangesAsync();
                        logger.LogInformation($"更改了 PlayerName：{oldName} --> {discordPlayer.MinecraftPlayer.PlayerName}");
                    }
                }

                player.Username = discordPlayer.Username;
                player.IdentifyNumber = discordPlayer.IdentifyNumber;
                player.MinecraftUuid = discordPlayer.MinecraftPlayer.Uuid;
                player.MinecraftPlayerName = discordPlayer.MinecraftPlayer.PlayerName;
                player.Element = discordPlayer.Element;
                player.IsGuard = discordPlayer.IsGuard;
                player.From = "discord";
            }
            // 开黑啦用户
            else if (discordPlayer is null)
            {
                logger.LogInformation("找到玩家属于 Kaiheila 平台");
                if (qString.AllKeys.Contains("player_name") is true)
                {
                    var oldName = kaiheilaPlayer.MinecraftPlayer.PlayerName;
                    if (qString["player_name"] != kaiheilaPlayer.MinecraftPlayer.PlayerName)
                    {
                        kaiheilaPlayer.MinecraftPlayer.PlayerName = qString["player_name"];
                        db.KaiheilaUsers.Update(kaiheilaPlayer);
                        await db.SaveChangesAsync();
                        logger.LogInformation($"更改了 PlayerName：{oldName} --> {kaiheilaPlayer.MinecraftPlayer.PlayerName}");
                    }
                }

                player.Username = kaiheilaPlayer.Username;
                player.IdentifyNumber = kaiheilaPlayer.IdentifyNumber;
                player.MinecraftUuid = kaiheilaPlayer.MinecraftPlayer.Uuid;
                player.MinecraftPlayerName = kaiheilaPlayer.MinecraftPlayer.PlayerName;
                player.Element = ElementHelper.GetElementFromKaiheila(kaiheilaPlayer.Roles);
                player.IsGuard = ElementHelper.IsGuardFromKaiheila(kaiheilaPlayer.Roles);
                player.From = "kaiheila";
            }

            response.StatusCode = HttpStatusCode.OK;
            await response.WriteStringAsync(JsonSerializer.Serialize(new HttpResponse()
            {
                Code = 0,
                Message = "请求成功",
                Data = player
            }));
            logger.LogInformation($"在 {player.From} 平台检索到玩家 {player.Username}#{player.IdentifyNumber} " +
                                  $"UUID = {player.MinecraftUuid}，Name = {player.MinecraftPlayerName}");
            return response;
        }
    }
}
