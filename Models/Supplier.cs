using System.ComponentModel.DataAnnotations;

namespace ControlInventarioMovil.Models
{
    public class Supplier
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int InventoryId { get; set; }

        [Required]
        [StringLength(50)]
        public string Ruc { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string BusinessName { get; set; } = string.Empty;

        public string? ContactName { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }

        [Required]
        public int StatusId { get; set; }
    }
}