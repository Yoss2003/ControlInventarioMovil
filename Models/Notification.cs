using System.ComponentModel.DataAnnotations;

namespace ControlInventarioMovil.Models
{
    public class Notification
    {
        [Key]
        public int Id { get; set; }

        [StringLength(255)]
        public string? NotificationName { get; set; }
    }
}