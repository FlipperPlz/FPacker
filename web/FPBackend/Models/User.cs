using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace FPBackend.Models
{
    [Table("User")]
    public class User
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)] public Guid Id { get; set; } 
        [DataType(DataType.EmailAddress)] public string Email { get; set; } = string.Empty;
        public bool Administrator { get; set; } = false;
        [DataType(DataType.DateTime)] public DateTime AccountCreationDate { get; set; }
        public long Steam64 { get; set; }
        [JsonIgnore] public byte[] PasswordHash { get; set; } = null!;
        [JsonIgnore] public byte[] PasswordSalt { get; set; } = null!;
    }
}
