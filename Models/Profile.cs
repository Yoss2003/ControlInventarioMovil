using System.ComponentModel.DataAnnotations;

namespace ControlInventarioMovil.Models
{
    public class Profile
    {
        [Key]
        public int Id { get; set; }
        [Required] public string Username { get; set; } = string.Empty;
        public int? LanguageId { get; set; }
        public int? ThemeId { get; set; }
        public int? NotificationId { get; set; }
        public int? DateFormatId { get; set; }
        public int? CurrencyId { get; set; }
        public int? MeasurementUnitId { get; set; }
        public int? TimeZoneId { get; set; }
        public int? SalesModeId { get; set; }

        public bool UseAuthentication { get; set; }
        public bool SharedActivity { get; set; }
        public bool UseBarcodes { get; set; }
        public bool CalculateDevaluation { get; set; }
        public bool GenerateCodes { get; set; }
        public bool ApplyLateFee { get; set; }

        public double? LateFeePercentage { get; set; }
        public int? GraceDays { get; set; }
        public string? SmtpEmail { get; set; }
        public string? SmtpPassword { get; set; }
    }
}