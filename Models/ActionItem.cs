using System.ComponentModel.DataAnnotations;

namespace ControlInventarioMovil.Models
{
    public class ActionItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(255)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }
    }
}