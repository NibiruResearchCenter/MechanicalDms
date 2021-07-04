using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MechanicalDms.Functions.Models
{
    public class DiscordSlashCommand
    {
        [JsonPropertyName("token")]
        public string Token { get; set; }

        [JsonPropertyName("data")]
        public CommandPayloadData Data { get; set; }

        [JsonPropertyName("member")]
        public DiscordGuildMember Member { get; set; }
    }

    public class DiscordGuildMember
    {
        [JsonPropertyName("roles")]
        public List<string> Roles { get; set; }
        [JsonPropertyName("user")]
        public DiscordUser User { get; set; }
    }

    public class DiscordUser
    {
        [JsonPropertyName("discriminator")]
        public string Discriminator { get; set; }
        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("username")]
        public string Username { get; set; }
    }
    
    public class CommandPayloadData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("options")]
        public List<CommandOptions> Options { get; set; }
    }
    
    public class CommandOptions
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("type")]
        public int Type { get; set; }
        [JsonPropertyName("options")]
        public List<CommandOptions> Options { get; set; }
        [JsonPropertyName("value")]
        public string Value { get; set; }
    }
}