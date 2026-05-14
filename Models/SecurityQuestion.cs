using System.ComponentModel.DataAnnotations;

namespace ControlInventarioMovil.Models
{
    public class SecurityQuestion
    {
        [Key]
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public int Question1Id { get; set; }
        public string Question1 { get; set; } = string.Empty;
        public string Answer1 { get; set; } = string.Empty;
        public int Question2Id { get; set; }
        public string Question2 { get; set; } = string.Empty;
        public string Answer2 { get; set; } = string.Empty;
        public int Question3Id { get; set; }
        public string Question3 { get; set; } = string.Empty;
        public string Answer3 { get; set; } = string.Empty;
    }
}