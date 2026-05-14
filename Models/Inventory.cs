using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace ControlInventarioMovil.Models
{
    public class Inventory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(255)]
        public string InventoryName { get; set; } = string.Empty;

        [Required]
        public string CreationDate { get; set; } = string.Empty;

        public string? ModificationDate { get; set; }

        public int UserId { get; set; }

        public string Username { get; set; } = string.Empty;

        public string? Alias { get; set; }
    }
}