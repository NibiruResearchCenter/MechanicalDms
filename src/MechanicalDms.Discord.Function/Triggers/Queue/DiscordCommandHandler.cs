using KaiheilaBot.Core.Models.Objects;
using MechanicalDms.Common;
using MechanicalDms.Database;
using MechanicalDms.Database.Models;
using MechanicalDms.Discord.Function.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace MechanicalDms.Discord.Function.Triggers.Queue
{
    public static class DiscordCommandHandler
    {
        [StorageAccount("AZURE_STORAGE_CONNECTION_STRING")]
        [Function("DiscordCommandHandler")]
        public static async Task Run(
            [QueueTrigger("discord-slash-command-queue")]
            string myQueueItem,
            FunctionContext context)
        {
            var logger = context.GetLogger("DiscordCommandHandler");
            logger.LogInformation("Start to handle new discord command.");

            // Deserialize command object
            DiscordSlashCommand command;
            try
            {
                command = JsonSerializer.Deserialize<DiscordSlashCommand>(myQueueItem);
                if (command is null)
                {
                    throw new NullReferenceException("Deserialized result is null.");
                }
            }
            catch (Exception e)
            {
                logger.LogError($"ERROR: Deserialize command object failed. Exception: {e.Message}");
                return;
            }

            logger.LogInformation("Command object deserialize successfully.");

            // Initialize RestClient and RestRequest for discord api call
            var url = $"https://discord.com/api/v8/webhooks/{Environment.GetEnvironmentVariable("DISCORD_APPLICATION_ID")}/" +
                      $"{command.Token}/messages/@original";
            logger.LogInformation($"Token = {command.Token}");

            var restClient =
                new RestClient(new Uri(url));
            var restRequest = new RestRequest(Method.PATCH);
            restRequest.AddHeader("Content-Type", "application/json");

            // Resolve command
           switch(command.Data.Name)
            {
                case "account":
                    {
                        switch (command.Data.Options[0].Name)
                        {
                            // Register
                            case "reg":
                                {
                                    logger.LogInformation("\"/account reg <>\" command checked.");

                                    // Get DbContext
                                    await using var db = new DmsDbContext();

                                    // Check if user have already registered
                                    var user = db.DiscordUsers.FirstOrDefault(x => x.Uid == command.Member.User.Id);
                                    if (user is not null)
                                    {
                                        logger.LogWarning($"WARN: User {command.Member.User.Username}#{command.Member.User.Discriminator} " +
                                                          $"({command.Member.User.Id}) exist in database, refuse new registration.");
                                        restRequest.AddJsonBody(new Dictionary<string, string>()
                                        {
                                            {"content", "You have already registered."}
                                        });
                                        var response = await restClient.ExecuteAsync(restRequest);
                                        logger.LogInformation("HTTP: " + response.Content);
                                        return;
                                    }

                                    // Get parameter
                                    string elementParam;
                                    try
                                    {
                                        elementParam = command.Data.Options[0].Options[0].Value;
                                    }
                                    catch (Exception e)
                                    {
                                        restRequest.AddJsonBody(new Dictionary<string, string>()
                                        {
                                            {"content", "Unknown command."}
                                        });
                                        var response = await restClient.ExecuteAsync(restRequest);
                                        logger.LogInformation("HTTP: " + response.Content);
                                        logger.LogError($"ERROR: Failed to get element param, user input error, {e.Message}");
                                        return;
                                    }
                                    logger.LogInformation($"Get <element param> successfully. Element = {elementParam}.");

                                    // Add to database
                                    var discordUser = new Database.Models.DiscordUser()
                                    {
                                        Uid = command.Member.User.Id,
                                        IdentifyNumber = command.Member.User.Discriminator,
                                        Username = command.Member.User.Username,
                                        Element = ElementHelper.GetElementFromString(elementParam),
                                        IsGuard = ElementHelper.IsGuardFromDiscord(string.Join(' ', command.Member.Roles))
                                    };
                                    try
                                    {
                                        db.DiscordUsers.Add(discordUser);
                                        await db.SaveChangesAsync();
                                        logger.LogInformation("Database updated. Return message.");
                                    }
                                    catch (Exception e)
                                    {
                                        logger.LogError(e.Message);
                                        restRequest.AddJsonBody(new Dictionary<string, string>()
                                        {
                                            {
                                                "content", $"Internal Error - Maybe you are already exist in database."
                                            }
                                        });
                                        var errRes = await restClient.ExecuteAsync(restRequest);
                                        logger.LogInformation("HTTP: " + errRes.Content);
                                        return;
                                    }

                                    // Patch slash command response message
                                    restRequest.AddJsonBody(new Dictionary<string, string>()
                                    {
                                        {
                                            "content", $"Register successfully.\n" +
                                                       $"UID = {discordUser.Uid}.\n" +
                                                       $"Element = {ElementHelper.GetElementString(discordUser.Element)} {ElementHelper.GetGuardString(discordUser.IsGuard)}.\n" +
                                                       $"Your role in discord will be updated when the server is synchronized.\n" +
                                                       $"You can get your profile info by execute command \"/account query\".\n"
                                        }
                                    });
                                    var res1 = await restClient.ExecuteAsync(restRequest);
                                    logger.LogInformation("HTTP: " + res1.Content);
                                    return;
                                }
                            // Query
                            case "query":
                                {
                                    logger.LogInformation("\"/account query\" command checked.");

                                    // Get user data from database
                                    await using var db = new DmsDbContext();
                                    var user = db.DiscordUsers
                                        .Include(x => x.MinecraftPlayer)
                                        .AsNoTracking()
                                        .FirstOrDefault(x => x.Uid == command.Member.User.Id);
                                    var returnValue = new Dictionary<string, string>();

                                    if (user is null)
                                    {
                                        // User is not exist
                                        logger.LogInformation($"Can not find user in database, uid = {command.Member.User.Id}, " +
                                                              $"username = {command.Member.User.Username}#{command.Member.User.Discriminator}");
                                        returnValue.Add("content", "Unable to find your data in the database");
                                    }
                                    else
                                    {
                                        // User exist
                                        logger.LogInformation($"Get user data from database successfully, uid = {command.Member.User.Id}, " +
                                                              $"username = {command.Member.User.Username}#{command.Member.User.Discriminator}");

                                        // Check minecraft player profile exist or not
                                        var mcUuid = user.MinecraftPlayer is null ? "NaN" : user.MinecraftPlayer.Uuid;
                                        var mcUsername = user.MinecraftPlayer is null ? "NaN" : user.MinecraftPlayer.PlayerName;
                                        var mcAccountStatus = user.MinecraftPlayer is null ? "NaN" : (user.MinecraftPlayer.IsLegitCopy ? "Premium" : "Cracked");
                                        logger.LogInformation($"User minecraft profile (uuid, name, legit) = ({mcUuid}, {mcUsername}, {mcAccountStatus})");
                                        returnValue.Add("content", $"Your data has been found in the database.\n" +
                                                                   $"Discord Username = {user.Username}#{user.IdentifyNumber}\n" +
                                                                   $"Discord UID = {user.Uid}\n" +
                                                                   $"Chattor Element = {ElementHelper.GetElementString(user.Element)} {ElementHelper.GetGuardString(user.IsGuard)}\n" +
                                                                   $"Minecraft UUID = {mcUuid}\n" +
                                                                   $"Minecraft Username = {mcUsername}\n" +
                                                                   $"Minecraft Account = {mcAccountStatus}");
                                    }

                                    // Patch slash command response message
                                    restRequest.AddJsonBody(returnValue);
                                    var response = await restClient.ExecuteAsync(restRequest);
                                    logger.LogInformation("HTTP: " + response.Content);
                                    return;
                                }
                            // Invalid command call
                            default:
                                logger.LogError("Unknown command.");
                                restRequest.AddJsonBody(new Dictionary<string, string>()
                            {
                                {"content", "Unknown command."}
                            });
                                var res2 = await restClient.ExecuteAsync(restRequest);
                                logger.LogInformation("HTTP: " + res2.Content);
                                break;
                        }
                    }
                    break;
                case "account-mod":
                    {
                        switch (command.Data.Options[0].Name)
                        {
                            // Get info
                            case "info":
                                switch (command.Data.Options[0].Options[0].Name)
                                {
                                    case "statistic":
                                        {
                                            await using var db = new DmsDbContext();
                                            var users = db.DiscordUsers
                                                .AsNoTracking()
                                                .Include(x => x.MinecraftPlayer)
                                                .ToList();
                                            var userCount = users.Count;
                                            var energyCount = users.Where(x => x.IsGuard).ToList().Count;
                                            var windCount = users.Where(x => x.Element == 2).ToList().Count;
                                            var aquaCount = users.Where(x => x.Element == 3).ToList().Count;
                                            var fireCount = users.Where(x => x.Element == 4).ToList().Count;
                                            var earthCount = users.Where(x => x.Element == 5).ToList().Count;
                                            var mcPlayerCount = users.Where(x => x.MinecraftPlayer is not null).ToList().Count;
                                            var errorCount = users.Where(x => x.SyncError is true).ToList().Count;
                                            restRequest.AddJsonBody(JsonSerializer.Serialize(new Dictionary<string, string>()
                                            {
                                                {"content", $"Statistic data:\n" +
                                                            $"Users: {userCount}\n" +
                                                            $"Energy Role: {energyCount}\n" +
                                                            $"Wind Role: {windCount}\n" +
                                                            $"Aqua Role: {aquaCount}\n" +
                                                            $"Fire Role: {fireCount}\n" +
                                                            $"Earth Role: {earthCount}\n" +
                                                            $"Sync Errors: {errorCount}\n" +
                                                            $"Minecraft Players: {mcPlayerCount}"}
                                            }));
                                            var response = await restClient.ExecuteAsync(restRequest);
                                            logger.LogInformation("HTTP: " + response.Content);
                                            return;
                                        }
                                    case "error":
                                        {
                                            await using var db = new DmsDbContext();
                                            var errors = db.DiscordUsers
                                                .AsNoTracking()
                                                .Where(x => x.SyncError)
                                                .ToList();

                                            var errorMessage = "";
                                            foreach (var errorUser in errors)
                                            {
                                                errorMessage += $"\nError: {errorUser.Uid} ({errorUser.Username}#{errorUser.IdentifyNumber})";
                                            }
                                            if (errorMessage == "")
                                            {
                                                errorMessage = "\nNo error";
                                            }

                                            restRequest.AddJsonBody(JsonSerializer.Serialize(new Dictionary<string, string>()
                                            {
                                                {"content", $"Total errors: {errors.Count}\n" + errorMessage}
                                            }));
                                            var response = await restClient.ExecuteAsync(restRequest);
                                            logger.LogInformation("HTTP: " + response.Content);
                                            return;
                                        }
                                }
                                break;
                            case "trigger":
                                switch (command.Data.Options[0].Options[0].Name)
                                {
                                    case "sync":
                                        {
                                            var functionUrl = Environment.GetEnvironmentVariable("SYNC_FUNCTION_URL");
                                            var functionKey = Environment.GetEnvironmentVariable("SYNC_FUNCTION_KEY");
                                            var azureTrigger = new RestClient(new Uri(functionUrl));
                                            var azureTriggerRequest = new RestRequest(Method.POST);
                                            azureTriggerRequest.AddHeader("x-functions-key", functionKey);
                                            azureTriggerRequest.AddHeader("Content-Type", "application/json");
                                            azureTriggerRequest.AddJsonBody("{}");
                                            var azureTriggerResult = await azureTrigger.ExecuteAsync(azureTriggerRequest);
                                            var responseData = new Dictionary<string, string>();
                                            if (azureTriggerResult.StatusCode == HttpStatusCode.Accepted)
                                            {
                                                var appIdParam = azureTriggerResult.Headers.FirstOrDefault(x => x.Name == "Request-Context");
                                                var appId = "Failed to get app id.";
                                                if (appIdParam is not null)
                                                {
                                                    appId = (string)appIdParam.Value;
                                                }
                                                responseData.Add("content", $"Success. Function is running.\n{appId}");
                                            }
                                            else
                                            {
                                                responseData.Add("content", $"Failed.\n" +
                                                                                $"Status Code: {azureTriggerResult.StatusCode}\n" +
                                                                                $"Response Message: {azureTriggerResult.Content}");
                                            }
                                            restRequest.AddJsonBody(responseData);
                                            var response = await restClient.ExecuteAsync(restRequest);
                                            logger.LogInformation("HTTP: " + response.Content);
                                            return;
                                        }
                                    case "delete-error":
                                        {
                                            await using var db = new DmsDbContext();
                                            var errorUsers = db.DiscordUsers
                                                .Include(x => x.MinecraftPlayer)
                                                .Where(x => x.SyncError)
                                                .ToList();

                                            var errorUserMinecraftPlayers = errorUsers
                                                .Where(x => x.MinecraftPlayer is not null)
                                                .Select(x => x.MinecraftPlayer)
                                                .ToList();

                                            var errorUserMinecraftPlayerUuids = errorUserMinecraftPlayers
                                                .Select(x => x.Uuid)
                                                .ToList();

                                            var errorUserCrackedMinecraftPlayers = db.MDAuthCrackedPlayers.ToList();
                                            errorUserCrackedMinecraftPlayers.RemoveAll
                                                (x => errorUserMinecraftPlayerUuids.Contains(x.Uuid));

                                            var removedErrorUsers = "";

                                            foreach(var user in errorUsers)
                                            {
                                                var mc = user.MinecraftPlayer is null ? "NULL" : $"{user.MinecraftPlayer.Uuid} ({user.MinecraftPlayer.PlayerName})";
                                                var cracked = user.MinecraftPlayer is null ? "NULL" :
                                                    (errorUserCrackedMinecraftPlayers.FirstOrDefault(x => x.Uuid == user.MinecraftPlayer.Uuid) is null ? "PREMIUM" : "CRACKED");
                                                var message = $"{user.Uid} ({user.Username}#{user.IdentifyNumber})\n" +
                                                                    $"MinecraftPlayer - {mc}\n" +
                                                                    $"CrackedMinecraftPlayer - {cracked}\n" +
                                                                    $"Element - {user.Element}\n" +
                                                                    $"IsGuard - {user.IsGuard}\n\n";
                                                logger.LogInformation($"DELETE MESSAGE: {message}");
                                                removedErrorUsers += message;
                                            }

                                            if (removedErrorUsers == "")
                                            {
                                                removedErrorUsers = "No Data.";
                                            }

                                            // Remove from MDAuthCrackedPlayers
                                            db.MDAuthCrackedPlayers.RemoveRange(errorUserCrackedMinecraftPlayers);
                                            // Remove from DiscordUsers
                                            db.DiscordUsers.RemoveRange(errorUsers);
                                            // Remove from MinecraftPlayer
                                            db.MinecraftPlayers.RemoveRange(errorUserMinecraftPlayers);

                                            var dbChanges = await db.SaveChangesAsync();

                                            restRequest.AddJsonBody(JsonSerializer.Serialize(new Dictionary<string, string>()
                                            {
                                                {"content", $"Database changes: {dbChanges}\n" +
                                                            $"Removed:\n\n{removedErrorUsers}" }
                                            }));
                                            var response = await restClient.ExecuteAsync(restRequest);
                                            logger.LogInformation("HTTP: " + response.Content);
                                            return;
                                        }
                                }
                                break;
                        }
                    }
                    break;
                default:
                    logger.LogInformation("Command verify failed, Exit.");
                    restRequest.AddJsonBody(new Dictionary<string, string>()
                    {
                        {"content", "Unknown command."}
                    });
                    var res = await restClient.ExecuteAsync(restRequest);
                    logger.LogInformation("HTTP: " + res.Content);
                    return;
            }
        }
    }
}
