using System.ComponentModel.DataAnnotations;

namespace FPBackend.Models.DTO; 

public record JwtResponseDTO (
    [DataType(DataType.Password)] string Token,
    [DataType(DataType.DateTime)] DateTime Expiration
);