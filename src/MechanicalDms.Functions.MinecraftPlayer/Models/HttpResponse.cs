using System.Text.Json.Serialization;

namespace MechanicalDms.Functions.MinecraftPlayer.Models
{
    public record HttpResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }
        
        [JsonPropertyName("data")]
        public Player Data { get; set; }
    }
}
