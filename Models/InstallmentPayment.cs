using System.ComponentModel.DataAnnotations;

namespace ControlInventarioMovil.Models
{
    public class InstallmentPayment
    {
        [Key]
        public int Id { get; set; }
        [Required] public int InstallmentId { get; set; }
        public double PaymentAmount { get; set; }
        [Required] public string PaymentDate { get; set; } = string.Empty;
        public string? Observation { get; set; }
    }
}