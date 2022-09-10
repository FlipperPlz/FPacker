using System.ComponentModel.DataAnnotations;

namespace FPBackend.Models.DTO
{
    public class UserCredentialsDTO
    {
        [Required(ErrorMessage = "Email is required")] public string Email { get; set; } = string.Empty;
        [Required(ErrorMessage = "Password is required")] public string Password { get; set; } = string.Empty;
    }
}
