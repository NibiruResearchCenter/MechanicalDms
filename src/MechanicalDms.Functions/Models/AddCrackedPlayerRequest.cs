using System.Text.Json.Serialization;

namespace MechanicalDms.Functions.Models
{
    public class AddCrackedPlayerRequest
    {
        [JsonPropertyName("uuid")]
        public string Uuid { get; set; }
        [JsonPropertyName("password")]
        public string Password { get; set; }
    }
}