using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using MechanicalDms.Functions.MinecraftPlayer.Models;
using MechanicalDms.Operation;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace MechanicalDms.Functions.MinecraftPlayer
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

            if (qString.AllKeys.Contains("uuid") is not true || qString.AllKeys.Contains("player_name") is not true)
            {
                logger.LogWarning("不存在 UUID 或 PlayerName 检索字符串");
                response.StatusCode = HttpStatusCode.NotFound;
                await response.WriteStringAsync(JsonSerializer.Serialize(new HttpResponse()
                {
                    Code = -100,
                    Message = "不存在 UUID 或 PlayerName 检索字符串",
                    Data = null
                }));
                return response;
            }

            using var kaiheilaUserOperation = new KaiheilaUserOperation();
            using var minecraftPlayerOperation = new MinecraftPlayerOperation();
            var player = kaiheilaUserOperation.GetKaiheilaUserByMinecraftUuid(qString["uuid"]);

            if (player is null)
            {
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

            if (qString["player_name"] != player.MinecraftPlayer.PlayerName)
            {
                player.MinecraftPlayer.PlayerName = qString["player_name"];
            }
            minecraftPlayerOperation.UpdateAndSave(player.MinecraftPlayer);
            
            response.StatusCode = HttpStatusCode.OK;
            await response.WriteStringAsync(JsonSerializer.Serialize(new HttpResponse()
            {
                Code = 0,
                Message = "请求成功",
                Data = new Models.MinecraftPlayer()
                {
                    KaiheilaUsername = player.Username,
                    KaiheilaUserIdentifyNumber = player.IdentifyNumber,
                    PlayerName = player.MinecraftPlayer.PlayerName,
                    Uuid = player.MinecraftPlayer.Uuid
                }
            }));
            logger.LogInformation($"检索到玩家 {player.Username}#{player.IdentifyNumber} " +
                                  $"UUID = {player.MinecraftPlayer.Uuid}，Name = {player.MinecraftPlayer.PlayerName}");
            return response;
        }
    }
}
