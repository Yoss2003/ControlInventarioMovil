using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace ControlInventarioMovil.Models
{
    public class User
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public int Age { get; set; }
        public string BirthDate { get; set; } = string.Empty;
        public string HireDate { get; set; } = string.Empty;
        public int JobPositionId { get; set; }
        public int AreaId { get; set; }
        public int ContractTypeId { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public string ProfilePictureUrl { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string RoleName { get; set; } = string.Empty;
    }
}