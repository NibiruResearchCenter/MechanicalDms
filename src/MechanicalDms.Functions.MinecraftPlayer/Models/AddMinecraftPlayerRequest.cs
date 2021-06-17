using System.Text.Json.Serialization;

namespace MechanicalDms.Functions.MinecraftPlayer.Models
{
    public class AddMinecraftPlayerRequest
    {
        [JsonPropertyName("kaiheila_uid")]
        public string KaiheilaUid { get; set; }
        
        [JsonPropertyName("uuid")]
        public string Uuid { get; set; }
        
        [JsonPropertyName("player_name")]
        public string PlayerName { get; set; }
    }
}