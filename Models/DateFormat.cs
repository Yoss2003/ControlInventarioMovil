using System.ComponentModel.DataAnnotations;

namespace ControlInventarioMovil.Models
{
    public class DateFormat
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string FormatName { get; set; } = string.Empty;
    }
}