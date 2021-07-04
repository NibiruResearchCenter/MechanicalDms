using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MechanicalDms.Database.Models
{
    [Table("MinecraftPlayer", Schema = "mechanical_dms")]
    public record MinecraftPlayer
    {
        [Required, Key]
        public string Uuid { get; set; }
        [Required]
        public string PlayerName { get; set; }
        [Required]
        public bool IsLegitCopy { get; set; }
    }
}
