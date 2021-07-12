using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MechanicalDms.Database.Models
{
    [Table("KaiheilaUser", Schema = "mechanical_dms")]
    public record KaiheilaUser
    {
        [Required, Key]
        public string Uid { get; set; }
        [Required]
        public string Username { get; set; }
        [Required]
        public string IdentifyNumber { get; set; }
        [Required]
        public string Roles { get; set; }
        [Required, DefaultValue(false)]
        public bool SyncError { get; set; }

        public BilibiliUser BilibiliUser { get; set; }
        public MinecraftPlayer MinecraftPlayer { get; set; }
    }
}
