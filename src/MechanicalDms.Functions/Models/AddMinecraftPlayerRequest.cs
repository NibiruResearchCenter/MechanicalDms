using System.Text.Json.Serialization;

namespace MechanicalDms.Functions.Models
{
    public class AddMinecraftPlayerRequest
    {
        [JsonPropertyName("uid")]
        public string Uid { get; set; }
        
        [JsonPropertyName("platform")] 
        public string Platform { get; set; }
        
        [JsonPropertyName("uuid")]
        public string Uuid { get; set; }
        
        [JsonPropertyName("player_name")]
        public string PlayerName { get; set; }
        
        [JsonPropertyName("is_legit_copy")]
        public bool IsLegitCopy { get; set; }
    }
}