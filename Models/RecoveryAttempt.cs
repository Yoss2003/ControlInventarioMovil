using System.ComponentModel.DataAnnotations;

namespace ControlInventarioMovil.Models
{
    public class RecoveryAttempt
    {
        [Key]
        public int Id { get; set; }
        [Required] public string Username { get; set; } = string.Empty;
        public int FailedAttempts { get; set; } = 0;
        public string? LastAttemptDate { get; set; }
        public string? BlockedUntil { get; set; }
    }
}