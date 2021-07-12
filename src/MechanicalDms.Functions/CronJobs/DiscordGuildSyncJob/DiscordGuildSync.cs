using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using MechanicalDms.Database;
using MechanicalDms.Functions.Common;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace MechanicalDms.Functions.CronJobs.DiscordGuildSyncJob
{
    public static class DiscordGuildSync
    {
        [Function("DiscordGuildSync")]
        public static async Task Run([TimerTrigger("0 0 */6 * * *")] MyInfo myTimer, FunctionContext context)
        {
            var logger = context.GetLogger("DiscordGuildSync");
            logger.LogInformation("Start to sync discord guild.");

            // Environment variables
            var token = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN");
            var guildId = Convert.ToUInt64(Environment.GetEnvironmentVariable("DISCORD_GUILD_ID"));
            var logChannelId = Convert.ToUInt64(Environment.GetEnvironmentVariable("DISCORD_LOG_CHANNEL_ID"));
            
            // Query database
            await using var db = new DmsDbContext();
            var discordUsers = db.DiscordUsers.ToList();

            // Discord bot
            var client = new DiscordSocketClient();
            client.Log += (message) =>
            {
                var exceptionMessage = message.Exception is not null ? message.Exception.Message : "";
                var trace = (message.Exception is not null ? message.Exception.StackTrace : "") ?? "";
                var log = $"{message.Source} > {message.Message} # {exceptionMessage} # {trace}";
                // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
                switch (message.Severity)
                {
                    case LogSeverity.Info:
                        logger.LogInformation(log);
                        break;
                    case LogSeverity.Warning:
                        logger.LogWarning(log);
                        break;
                    case LogSeverity.Error:
                        logger.LogError(log);
                        break;
                    case LogSeverity.Critical:
                        logger.LogCritical(log);
                        break;
                    case LogSeverity.Verbose:
                        logger.LogTrace(log);
                        break;
                    case LogSeverity.Debug:
                        logger.LogDebug(log);
                        break;
                }
                return Task.CompletedTask;
            };
            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();

            await Task.Delay(5000);

            if (client.ConnectionState is not ConnectionState.Connected) 
            {
                logger.LogCritical("Discord connect failed.");
                await client.StopAsync();
                await client.LogoutAsync();
                await Task.Delay(100);
                return;
            }

            var rest = client.Rest;

            // Get guild
            var guild = await rest.GetGuildAsync(guildId);
            if (guild is null)
            {
                logger.LogCritical("Get guild failed.");
                await client.StopAsync();
                await client.LogoutAsync();
                await Task.Delay(100);
                return;
            }

            // Update database and set role
            var sw = new Stopwatch();
            sw.Start();
            var errorUserList = new Dictionary<string, string>();
            foreach (var discordUser in discordUsers)
            {
                var remoteUser = await guild.GetUserAsync(Convert.ToUInt64(discordUser.Uid));
                if (remoteUser is null)
                {
                    errorUserList.Add(discordUser.Uid, discordUser.Username + "#" + discordUser.IdentifyNumber);
                    logger.LogWarning($"Failed to get user {discordUser.Username}#{discordUser.IdentifyNumber} " +
                                      $"with uid {discordUser.Uid}");
                    continue;
                }
                discordUser.Username = remoteUser.Username;
                discordUser.IdentifyNumber = remoteUser.Discriminator;
                var roleIdList = new List<string>();
                foreach(var role in remoteUser.RoleIds)
                {
                    roleIdList.Add(role.ToString());
                }
                discordUser.IsGuard = ElementHelper.IsGuardFromDiscord(string.Join(' ', roleIdList));
                if (ElementHelper.GetElementFromDiscord(string.Join(' ', roleIdList)) == 0)
                {
                    var roleId = ElementHelper.GetElementRoleForDiscord(discordUser.Element);
                    await remoteUser.AddRoleAsync(roleId);
                    logger.LogInformation($"Add role {ElementHelper.GetElementString(discordUser.Element)}({roleId}) to " +
                                          $"user {discordUser.Username}#{discordUser.IdentifyNumber} with uid {discordUser.Uid}");
                }
                db.DiscordUsers.Update(discordUser);
            }
            var changes = await db.SaveChangesAsync();
            sw.Stop();
            
            // Send log
            var errorMessage = "";
            foreach (var (key, value) in errorUserList)
            {
                if (errorMessage != "")
                {
                    errorMessage += "\n";
                }
                errorMessage += $"Error: {key} ({value})";
            }
            if (errorMessage == "")
            {
                errorMessage = "No error";
            }

            var logMessage = $"Operation log , UTC Time: {DateTime.Now.ToUniversalTime()}\n" +
                             $"Azure functions operation id: {context.InvocationId}\n" +
                             $"Main operation time: {sw.ElapsedMilliseconds} ms\n" +
                             $"Total database changes: {changes}\n" +
                             $"{errorMessage}";

            var channel = await guild.GetTextChannelAsync(logChannelId);
            await channel.SendMessageAsync(logMessage);

            // Discord logout
            rest = null;
            await client.StopAsync();
            await client.LogoutAsync();
            await Task.Delay(100);
        }
    }

    public class MyInfo
    {
        public MyScheduleStatus ScheduleStatus { get; set; }

        public bool IsPastDue { get; set; }
    }

    public class MyScheduleStatus
    {
        public DateTime Last { get; set; }

        public DateTime Next { get; set; }

        public DateTime LastUpdated { get; set; }
    }
}