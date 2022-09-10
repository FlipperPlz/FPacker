using FPBackend.Models;
using FPBackend.Models.DTO;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using FPBackend.Context;

namespace FPBackend.Controllers
{
    [Route("api/authentication")]
    [ApiController]
    public class AuthController : ControllerBase {
        private readonly FPDataContext _context;
        
        public AuthController(FPDataContext context) => _context = context;

        [HttpPost("register")]
        public async Task<ActionResult<User>> Register(UserRegistrationDTO credentials) {
            if (_context.Users.Where(e => e.Email.ToLower() == credentials.EmailAddress.ToLower()).ToList().Count > 0)
                return BadRequest("A user with this email already exists");
            var passwdHashTuple = CreatePasswordHash(credentials.Password);
            var newUser = new User() {
                AccountCreationDate = DateTime.Now,
                Administrator = false,
                Email = credentials.EmailAddress,
                Steam64 = credentials.Steam64ID,
                PasswordSalt = passwdHashTuple.Item1,
                PasswordHash = passwdHashTuple.Item2
            };
            await _context.Users.AddAsync(newUser);
            await _context.SaveChangesAsync();
            return Ok(newUser);
        }

        [HttpPost("login")]
        public async Task<ActionResult<string>> Login(UserCredentialsDTO credentials) {
            if (_context.Users.Where(u => string.Equals(u.Email, credentials.Email, StringComparison.CurrentCultureIgnoreCase)).ToList().Count == 0) return BadRequest("Invalid Credentials");
            var foundUser = _context.Users.First(u => string.Equals(u.Email, credentials.Email, StringComparison.CurrentCultureIgnoreCase));
            if (!VerifyPasswordHash(credentials.Password, foundUser.PasswordHash, foundUser.PasswordSalt)) return BadRequest("Invalid Credentials");
            return Ok();
        }

        private (byte[], byte[]) CreatePasswordHash(string password)
        {
            using var hmac = new HMACSHA512();
            return (hmac.Key, hmac.ComputeHash(Encoding.UTF8.GetBytes(password)));
        }

        private bool VerifyPasswordHash(string password, byte[] hash, byte[] salt) {
            using var hmac = new HMACSHA512(salt);
            var computed = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
            return computed.SequenceEqual(hash);
        }
    }
}
