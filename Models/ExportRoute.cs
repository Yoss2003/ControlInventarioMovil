using System.ComponentModel.DataAnnotations;

namespace ControlInventarioMovil.Models
{
    public class ExportRoute
    {
        [Key]
        public int Id { get; set; }
        public int UserId { get; set; }
        public string? DefaultRoute1 { get; set; }
        public string? CustomRoute1 { get; set; }
        public string? DefaultRoute2 { get; set; }
        public string? CustomRoute2 { get; set; }
        public string? FileType1 { get; set; }
        public string? FileType2 { get; set; }
        public bool IsDefault1 { get; set; }
        public bool IsDefault2 { get; set; }
    }
}