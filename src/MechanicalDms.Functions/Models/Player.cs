using System.Text.Json.Serialization;

namespace MechanicalDms.Functions.Models
{
    public record Player
    {
        [JsonPropertyName("username")]
        public string Username { get; set; }
        
        /// <summary>
        /// kaiheila / discord
        /// </summary>
        [JsonPropertyName("from")]
        public string From { get; set; }
        
        [JsonPropertyName("identify_number")]
        public string IdentifyNumber { get; set; }
        
        [JsonPropertyName("minecraft_uuid")]
        public string MinecraftUuid { get; set; }

        [JsonPropertyName("minecraft_player_name")]
        public string MinecraftPlayerName { get; set; }
        
        [JsonPropertyName("element")]
        public int Element { get; set; }
        
        [JsonPropertyName("is_guard")]
        public bool IsGuard { get; set; }
    }
}
