using System.ComponentModel.DataAnnotations;

namespace ControlInventarioMovil.Models
{
    // El "cerebro" de la flexibilidad del inventario
    public enum TrackingMode
    {
        Serializado = 0,
        Standard = 1,
        Bulk = 2
    }

    public class Article
    {
        [Key]
        public int Id { get; set; }
        public int InventoryId { get; set; }
        [Required] public string Code { get; set; } = string.Empty;
        public string? Barcode { get; set; } // El código de barras comercial (escaneable)
        [Required] public string Name { get; set; } = string.Empty; // Ej. "Laptop HP Pavilion"
        [Required] public string Model { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public int BrandId { get; set; }
        public TrackingMode Tracking { get; set; } = TrackingMode.Standard;
        public string MeasurementUnit { get; set; } = "Unidades";
        public decimal Stock { get; set; } = 0;
        public string? SerialNumber { get; set; }
        public int? CurrentEmployeeId { get; set; }
        public int? PreviousEmployeeId { get; set; }
        public string? FixedAsset { get; set; }
        public decimal? AcquisitionPrice { get; set; } // Cambiado a decimal (Estándar para dinero)
        public decimal? SalePrice { get; set; }        // Precio de venta/salida para la UI
        public string? AcquisitionCurrency { get; set; }
        public DateTime? AcquisitionDate { get; set; }
        public DateTime? DecommissionDate { get; set; }
        public DateTime? WarrantyEndDate { get; set; }
        public int StatusId { get; set; }
        public int LocationId { get; set; }
        public int ConditionId { get; set; }
        public string? Observation { get; set; }
        public string? MainPhotoPath { get; set; }
        public string? SecondaryPhotoPath { get; set; }
        public string? MainVoucherPath { get; set; }
        public string? SecondaryVoucherPath { get; set; }
        public string? SupplierRuc { get; set; }
        public string? SupplierName { get; set; }
        public int? UsefulLifeMonths { get; set; }
        public string? Characteristics { get; set; }
        public DateTime? RegistrationDate { get; set; }
        public DateTime? ModificationDate { get; set; }
        public DateTime? DepartureDate { get; set; }
        public int ActionId { get; set; }
        public int? RegistrationGroupId { get; set; }
    }
}