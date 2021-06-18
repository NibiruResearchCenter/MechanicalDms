using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using MechanicalDms.Functions.Models;
using MechanicalDms.Operation;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace MechanicalDms.Functions
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

                if (body.Uuid is null || body.PlayerName is null || body.KaiheilaUid is null)
                {
                    throw new JsonException("Deserialized result parameter is null");
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

            using var kaiheilaUserOperation = new KaiheilaUserOperation();
            using var minecraftPlayerOperation = new MinecraftPlayerOperation();

            var kaiheilaUser = kaiheilaUserOperation.GetKaiheilaUser(body.KaiheilaUid);
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
            
            minecraftPlayerOperation.AddOrUpdateMinecraftUser(body.Uuid, body.PlayerName);
            kaiheilaUserOperation.BindingMinecraft(body.KaiheilaUid, body.Uuid);

            logger.LogInformation($"请求成功，已添加 Minecraft Player UUID = {body.Uuid}，" +
                                  $"MinecraftPlayerName = {body.PlayerName}，开黑啦 UID = {body.KaiheilaUid}");
            response.StatusCode = HttpStatusCode.OK;
            await response.WriteStringAsync(JsonSerializer.Serialize(new HttpResponse()
            {
                Code = 0,
                Message = "请求成功",
                Data = new Player()
                {
                    KaiheilaUsername = kaiheilaUser.Username,
                    KaiheilaUserIdentifyNumber = kaiheilaUser.IdentifyNumber,
                    MinecraftPlayerName = body.PlayerName,
                    MinecraftUuid = body.Uuid,
                    BilibiliGuardLevel = kaiheilaUser.BilibiliUser.GuardLevel,
                    Element = CheckElement.Get(kaiheilaUser.Roles)
                }
            }));
            return response;
        }
    }
}
