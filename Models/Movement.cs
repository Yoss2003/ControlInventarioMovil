using System.ComponentModel.DataAnnotations;

namespace ControlInventarioMovil.Models
{
    public class Movement
    {
        [Key]
        public int Id { get; set; }
        [Required] public int ArticleId { get; set; }
        public int? EmployeeId { get; set; }
        [Required] public int ActionId { get; set; }
        [Required] public string MovementDate { get; set; } = string.Empty;
        public string? Observation { get; set; }
        public double? Amount { get; set; }
        public string? Recipient { get; set; }
        public double? SalePrice { get; set; }
        public string? DocumentNumber { get; set; }
        public string? PaymentMethod { get; set; }
        public string? VoucherType { get; set; }
        public string? CustomerPhone { get; set; }
        public string? CustomerEmail { get; set; }
    }
}