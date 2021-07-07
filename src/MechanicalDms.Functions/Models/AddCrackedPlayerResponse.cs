using System.Text.Json.Serialization;

namespace MechanicalDms.Functions.Models
{
    public class AddCrackedPlayerResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }
        [JsonPropertyName("uuid")]
        public string Uuid { get; set; }
    }
}