using System.Text.Json.Serialization;

namespace MechanicalDms.Functions.Models
{
    public class GetCrackedPlayerResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }
        [JsonPropertyName("verify_status")]
        public bool VerifyStatus { get; set; }
    }
}
