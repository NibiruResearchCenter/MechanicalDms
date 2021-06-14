using System.Text.Json.Serialization;

namespace MechanicalDms.AccountManager.Models
{
    public record Guard
    {
        [JsonPropertyName("Uid")]
        public long Uid { get; set; }

        [JsonPropertyName("Username")]
        public string Username { get; set; }

        [JsonPropertyName("GuardLevel")]
        public int GuardLevel { get; set; }
    }
}
