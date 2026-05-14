using System.ComponentModel.DataAnnotations;

namespace ControlInventarioMovil.Models
{
    public class Parameter
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int InventoryId { get; set; }

        [Required]
        [StringLength(100)]
        public string ParameterType { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }
    }
}