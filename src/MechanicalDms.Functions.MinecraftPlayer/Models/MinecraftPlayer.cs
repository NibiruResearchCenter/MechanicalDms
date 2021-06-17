using System.Text.Json.Serialization;

namespace MechanicalDms.Functions.MinecraftPlayer.Models
{
    public record MinecraftPlayer
    {
        [JsonPropertyName("kaiheila_username")]
        public string KaiheilaUsername { get; set; }
        
        [JsonPropertyName("kaiheila_user_identify_number")]
        public string KaiheilaUserIdentifyNumber { get; set; }
        
        [JsonPropertyName("uuid")]
        public string Uuid { get; set; }

        [JsonPropertyName("player_name")]
        public string PlayerName { get; set; }
    }
}
