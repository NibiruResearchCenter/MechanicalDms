using System.Text.Json.Serialization;

namespace MechanicalDms.Functions.Models
{
    public record Player
    {
        [JsonPropertyName("kaiheila_username")]
        public string KaiheilaUsername { get; set; }
        
        [JsonPropertyName("kaiheila_user_identify_number")]
        public string KaiheilaUserIdentifyNumber { get; set; }

        [JsonPropertyName("bilibili_guard_level")]
        public int BilibiliGuardLevel { get; set; }

        [JsonPropertyName("minecraft_uuid")]
        public string MinecraftUuid { get; set; }

        [JsonPropertyName("minecraft_player_name")]
        public string MinecraftPlayerName { get; set; }
    }
}
