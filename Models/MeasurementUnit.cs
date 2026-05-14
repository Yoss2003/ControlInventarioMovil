using System.ComponentModel.DataAnnotations;

namespace ControlInventarioMovil.Models
{
    public class MeasurementUnit
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string UnitName { get; set; } = string.Empty;
    }
}