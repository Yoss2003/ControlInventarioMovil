using System.ComponentModel.DataAnnotations;

namespace ControlInventarioMovil.Models
{
    public class Category
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int InventoryId { get; set; }

        [Required]
        [StringLength(255)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }
        public string? CreationDate { get; set; }
        public string? CreationUser { get; set; }
        public string? ModificationDate { get; set; }
        public string? ModificationUser { get; set; }
        public string? DeletionDate { get; set; }
        public string? DeletionUser { get; set; }

        [Required]
        public int IsReturnable { get; set; } = 1;
    }
}