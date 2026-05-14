using System.ComponentModel.DataAnnotations;

namespace ControlInventarioMovil.Models
{
    public class AccountReceivable
    {
        [Key]
        public int Id { get; set; }
        [Required] public int MovementId { get; set; }
        public int InstallmentNumber { get; set; }
        public double InstallmentAmount { get; set; }
        public double PaidAmount { get; set; } = 0;
        public double LateFeeAmount { get; set; } = 0;
        [Required] public string DueDate { get; set; } = string.Empty;
        public string? PaymentDate { get; set; }
        [Required] public string Status { get; set; } = "Pending";
        [Required] public string Frequency { get; set; } = string.Empty;
    }
}