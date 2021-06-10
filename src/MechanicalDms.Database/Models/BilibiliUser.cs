using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MechanicalDms.Database.Models
{
    [Table("BilibiliUser", Schema = "mechanical_dms")]
    public record BilibiliUser
    {
        [Required, Key]
        public long Uid { get; set; }
        [Required]
        public string Username { get; set; }
        [Required]
        public int GuardLevel { get; set; }
        [Required]
        public long ExpireTime { get; set; }
        [Required]
        public int Level { get; set; }
    }
}
