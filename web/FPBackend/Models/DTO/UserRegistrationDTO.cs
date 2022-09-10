using System.ComponentModel.DataAnnotations;

namespace FPBackend.Models.DTO; 

public class UserRegistrationDTO {
    [DataType(DataType.EmailAddress), Required(ErrorMessage = "Email address is required for registration")]
    public string EmailAddress { get; set; } = null!;
    
    [DataType(DataType.Password), Required(ErrorMessage = "A password is required for registration")]
    public string Password { get; set; } = null!;

    [DataType(DataType.Password), Compare("Password", ErrorMessage = "Passwords do not match")]
    [Required(ErrorMessage = "You are required to confirm your password")]
    public string ConfirmPassword { get; set; } = null!;
    
    [Required(ErrorMessage = "Your Steam64 ID is required.")]
    public long Steam64ID { get; set; }
}