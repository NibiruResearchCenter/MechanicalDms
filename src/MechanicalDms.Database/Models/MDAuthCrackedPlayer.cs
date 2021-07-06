using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MechanicalDms.Database.Models
{
    [Table("MDAuthCrackedPlayer", Schema = "mechanical_dms")]
    public record MDAuthCrackedPlayer
    {
        [Required, Key] 
        public string Uuid { get; set; }
        [Required]
        public string Password { get; set; }
    }
}
