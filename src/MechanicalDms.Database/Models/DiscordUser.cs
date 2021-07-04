using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MechanicalDms.Database.Models
{
    [Table("DiscordUser", Schema = "mechanical_dms")]
    public record DiscordUser
    {
        [Required, Key]
        public string Uid { get; set; }
        [Required]
        public string Username { get; set; }
        [Required]
        public string IdentifyNumber { get; set; }
        [Required]
        public int Element { get; set; }
        [Required]
        public bool IsGuard { get; set; }

        public MinecraftPlayer MinecraftPlayer { get; set; }
    }
}
