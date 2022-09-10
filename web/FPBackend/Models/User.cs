using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FPBackend.Models
{
    [Table("User")]
    public class User
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)] public int Id { get; set; } 
        [DataType(DataType.EmailAddress)] public string Email { get; set; } = string.Empty;
        public bool Administrator { get; set; } = false;
        [DataType(DataType.DateTime)] public DateTime AccountCreationDate { get; set; }
        public long Steam64 { get; set; }
        public byte[] PasswordHash { get; set; } = null!;
        public byte[] PasswordSalt { get; set; } = null!;
    }
}
