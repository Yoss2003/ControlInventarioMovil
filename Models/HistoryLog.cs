using System.ComponentModel.DataAnnotations;

namespace ControlInventarioMovil.Models
{
    public class HistoryLog
    {
        [Key]
        public int Id { get; set; }
        public DateTime? LogDate { get; set; }
        [Required] public string Username { get; set; } = string.Empty;
        [Required] public string ModuleName { get; set; } = string.Empty;
        [Required] public string ActionName { get; set; } = string.Empty;
        public string? Detail { get; set; }
    }
}