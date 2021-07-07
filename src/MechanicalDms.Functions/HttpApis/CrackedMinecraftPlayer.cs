using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using MechanicalDms.Database;
using MechanicalDms.Database.Models;
using MechanicalDms.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicalDms.Functions.HttpApis
{
    public static class CrackedMinecraftPlayer
    {
        [Function("CrackedMinecraftPlayer")]
        public static async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "minecraft-player/cracked")]
            HttpRequestData req,
            FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger("CrackedMinecraftPlayer");
            logger.LogInformation($"收到请求 Method = {req.Method}, URL = {req.Url}");
            var response = req.CreateResponse();

            await using var db = new DmsDbContext();
            
            switch (req.Method.ToLower())
            {
                case "get":
                {
                    logger.LogInformation("GET 方法，验证密码");
                    var qString = HttpUtility.ParseQueryString(req.Url.Query);
                    if (qString.AllKeys.Contains("uuid") is false && qString.AllKeys.Contains("pass"))
                    {
                        logger.LogError("ERROR: 不存在 uuid 或 pass 检索字符串");
                        response.StatusCode = HttpStatusCode.NotFound;
                        await response.WriteAsJsonAsync(new GetCrackedPlayerResponse()
                        {
                            Code = -1100,
                            VerifyStatus = false
                        });
                        return response;
                    }

                    var cPlayer = db.MDAuthCrackedPlayers
                        .AsNoTracking()
                        .FirstOrDefault(x => x.Uuid == qString["uuid"]);
                    if (cPlayer is null)
                    {
                        logger.LogError($"不存在该玩家，UUID = {qString["uuid"]}");
                        response.StatusCode = HttpStatusCode.NotFound;
                        await response.WriteAsJsonAsync(new GetCrackedPlayerResponse()
                        {
                            Code = -1200,
                            VerifyStatus = false
                        });
                        return response;
                    }

                    logger.LogInformation($"已找到玩家，密码验证结果 {(cPlayer.Password == qString["pass"]).ToString()}");
                    response.StatusCode = HttpStatusCode.OK;
                    await response.WriteAsJsonAsync(new GetCrackedPlayerResponse()
                    {
                        Code = 0,
                        VerifyStatus = cPlayer.Password == qString["pass"]
                    });
                    return response;
                }
                case "post":
                {
                    logger.LogInformation("POST 方法，添加用户");
                    var payload = await req.ReadAsStringAsync();
                    if (payload is null)
                    {
                        response.StatusCode = HttpStatusCode.BadRequest;
                        await response.WriteAsJsonAsync(new AddCrackedPlayerResponse()
                        {
                            Code = -1500,
                            Uuid = ""
                        });
                        return response;
                    }

                    AddCrackedPlayerRequest cPlayer;
                    try
                    {
                        cPlayer = JsonSerializer.Deserialize<AddCrackedPlayerRequest>(payload);
                        if (cPlayer is null)
                        {
                            throw new JsonException("Deserialized result is null");
                        }
                        if (cPlayer.Password is null || cPlayer.Uuid is null)
                        {
                            throw new JsonException("Deserialized result parameter is null");
                        }
                    }
                    catch (Exception e)
                    {
                        logger.LogError($"反序列化请求Body出错，{e.Message}");
                        response.StatusCode = HttpStatusCode.BadRequest;
                        await response.WriteAsJsonAsync(new AddCrackedPlayerResponse()
                        {
                            Code = -1600,
                            Uuid = ""
                        });
                        return response;
                    }

                    var exits = db.MDAuthCrackedPlayers
                        .AsNoTracking()
                        .FirstOrDefault(x => x.Uuid == cPlayer.Uuid) is not null;
                    
                    if (exits)
                    {
                        logger.LogInformation($"该玩家已存在，UUID = {cPlayer.Uuid}");
                        response.StatusCode = HttpStatusCode.BadRequest;
                        await response.WriteAsJsonAsync(new AddCrackedPlayerResponse()
                        {
                            Code = -1700,
                            Uuid = ""
                        });
                        return response;
                    }
                    
                    db.MDAuthCrackedPlayers.Add(new MDAuthCrackedPlayer()
                    {
                        Uuid = cPlayer.Uuid,
                        Password = cPlayer.Password
                    });
                    await db.SaveChangesAsync();

                    response.StatusCode = HttpStatusCode.OK;
                    await response.WriteAsJsonAsync(new AddCrackedPlayerResponse()
                    {
                        Code = 0,
                        Uuid = cPlayer.Uuid
                    });
                    logger.LogInformation($"Cracked Player 添加成功，UUID = {cPlayer.Uuid}");
                    return response;
                }
                default:
                    logger.LogError("未知错误");
                    response.StatusCode = HttpStatusCode.NotAcceptable;
                    await response.WriteAsJsonAsync(new AddCrackedPlayerResponse()
                    {
                        Code = -1800,
                        Uuid = ""
                    });
                    return response;
            }
        }
    }
}
