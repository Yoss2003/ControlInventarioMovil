using System.ComponentModel.DataAnnotations;

namespace ControlInventarioMovil.Models
{
    public class Customer
    {
        [Key]
        public int Id { get; set; }

        [StringLength(50)]
        public string? DocumentNumber { get; set; }

        [Required]
        [StringLength(255)]
        public string Name { get; set; } = string.Empty;

        [StringLength(50)]
        public string? Phone { get; set; }

        [StringLength(255)]
        public string? Email { get; set; }

        public string? Address { get; set; }

        [StringLength(255)]
        public string? RegistrationDate { get; set; }
    }
}