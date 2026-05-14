using System.ComponentModel.DataAnnotations;

namespace ControlInventarioMovil.Models
{
    public class Brand
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int InventoryId { get; set; }

        [Required]
        public int CategoryId { get; set; }

        [Required]
        [StringLength(255)]
        public string Name { get; set; } = string.Empty;
    }
}