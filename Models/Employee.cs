using System.ComponentModel.DataAnnotations;

namespace ControlInventarioMovil.Models
{
    public class Employee
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(255)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string DNI { get; set; } = string.Empty;

        [Required]
        public int JobPositionId { get; set; }

        [Required]
        public int AreaId { get; set; }

        [Required]
        public int StatusId { get; set; }
    }
}