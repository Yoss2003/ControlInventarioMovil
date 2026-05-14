using System.ComponentModel.DataAnnotations;

namespace ControlInventarioMovil.Models
{
    public class Article
    {
        [Key]
        public int Id { get; set; }
        public int InventoryId { get; set; }
        [Required] public string Code { get; set; } = string.Empty;
        [Required] public string Model { get; set; } = string.Empty;
        [Required] public string SerialNumber { get; set; } = string.Empty;
        public int BrandId { get; set; }
        public string AcquisitionDate { get; set; } = string.Empty;
        public string? DecommissionDate { get; set; }
        public string? WarrantyEndDate { get; set; }

        public int? CurrentEmployeeId { get; set; }
        public int? PreviousEmployeeId { get; set; }

        public int StatusId { get; set; }
        public int LocationId { get; set; }
        public int ConditionId { get; set; }
        public string? FixedAsset { get; set; }

        public string? Observation { get; set; }
        public string? MainPhotoPath { get; set; }
        public string? SecondaryPhotoPath { get; set; }
        public string? MainVoucherPath { get; set; }
        public string? SecondaryVoucherPath { get; set; }

        public string? SupplierRuc { get; set; }
        public string? SupplierName { get; set; }
        public double? AcquisitionPrice { get; set; }
        public string? AcquisitionCurrency { get; set; }
        public int? UsefulLifeMonths { get; set; }
        public string? Characteristics { get; set; }

        public int CategoryId { get; set; }
        public string? RegistrationDate { get; set; }
        public string? ModificationDate { get; set; }
        public string? DepartureDate { get; set; }
        public int ActionId { get; set; }

        public string? MeasurementUnit { get; set; }
        public int? RegistrationGroupId { get; set; }
    }
}