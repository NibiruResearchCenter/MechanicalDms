using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MechanicalDms.Database;
using MechanicalDms.Functions.Common;
using MechanicalDms.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestSharp;
using DiscordUser = MechanicalDms.Database.Models.DiscordUser;

namespace MechanicalDms.Functions.HttpApis
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
            logger.LogInformation("Start to handle new discord command");

            // Deserialize command object
            DiscordSlashCommand command;
            try
            {
                command = JsonSerializer.Deserialize<DiscordSlashCommand>(myQueueItem);
                if (command is null)
                {
                    throw new NullReferenceException("Deserialized result is null");
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

            // Verify command
            if (command.Data.Name != "account" || command.Data.Options.Count < 1)
            {
                logger.LogInformation("NOT \"/account\" command or not enough options, Exit.");
                restRequest.AddJsonBody(new Dictionary<string, string>()
                {
                    {"content", "Unknown command."}
                });
                var res = await restClient.ExecuteAsync(restRequest);
                logger.LogInformation("HTTP: " + res.Content);
                return;
            }

            // Select command options
            switch (command.Data.Options[0].Name)
            {
                // Register
                case "reg":
                {
                    logger.LogInformation("\"/account reg <>\" command checked.");
                    
                    // Get DbContext
                    await using var db = new DmsDbContext();

                    // Check if user have already registered
                    var user = db.DiscordUser.FirstOrDefault(x => x.Uid == command.Member.User.Id);
                    if (user is not null)
                    {
                        logger.LogWarning($"WARN: User {command.Member.User.Username}#{command.Member.User.Discriminator} " +
                                          $"({command.Member.User.Id}) exist in database, refuse new registration.");
                        restRequest.AddJsonBody(new Dictionary<string, string>()
                        {
                            {"content", "You have already registered."}
                        });
                        var res = await restClient.ExecuteAsync(restRequest);
                        logger.LogInformation("HTTP: " + res.Content);
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
                        var res = await restClient.ExecuteAsync(restRequest);
                        logger.LogInformation("HTTP: " + res.Content);
                        logger.LogError($"ERROR: Failed to get element param, user input error, {e.Message}");
                        return;
                    }
                    logger.LogInformation("Get <element param> successfully.");
                    
                    // Add to database
                    var discordUser = new DiscordUser()
                    {
                        Uid = command.Member.User.Id,
                        IdentifyNumber = command.Member.User.Discriminator,
                        Username = command.Member.User.Username,
                        Element = ElementHelper.GetElementFromString(elementParam),
                        IsGuard = ElementHelper.IsGuardFromDiscord(string.Join(' ', command.Member.Roles))
                    };
                    db.DiscordUser.Add(discordUser);
                    await db.SaveChangesAsync();
                    logger.LogInformation("Database updated. Return message.");
                    
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
                    var user = db.DiscordUser
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
                        var mcIsLegit = user.MinecraftPlayer is null ? "NaN" : user.MinecraftPlayer.IsLegitCopy.ToString();
                        logger.LogInformation($"User minecraft profile (uuid, name, legit) = ({mcUuid}, {mcUsername}, {mcIsLegit})");
                        returnValue.Add("content", $"Your data has been found in the database.\n" +
                                                   $"Discord Username = {user.Username}#{user.IdentifyNumber}\n" +
                                                   $"Discord UID = {user.Uid}\n" +
                                                   $"Element = {ElementHelper.GetElementString(user.Element)} {ElementHelper.GetGuardString(user.IsGuard)}\n" +
                                                   $"Minecraft UUID = {mcUuid}\n" +
                                                   $"Minecraft Username = {mcUsername}\n" +
                                                   $"Minecraft Is Legit Copy = {mcIsLegit}");
                    }
                    
                    // Patch slash command response message
                    restRequest.AddJsonBody(returnValue);
                    var res = await restClient.ExecuteAsync(restRequest);
                    logger.LogInformation("HTTP: " + res.Content);
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
    }
}