using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FPBackend.Models;
using FPBackend.Models.DTO;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using FPBackend.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace FPBackend.Controllers
{
    [Route("api/authentication")]
    [ApiController]
    public class AuthController : ControllerBase {
        private readonly FPDataContext _context;
        private readonly IConfiguration _configuration;
        
        public AuthController(FPDataContext context, IConfiguration config) {
            _context = context;
            _configuration = config;
        } 
        
        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<ActionResult<User>> Register(UserRegistrationDTO credentials) {
            if (_context.Users.ToList().Where(e => string.Equals(e.Email, credentials.EmailAddress, StringComparison.CurrentCultureIgnoreCase)).ToList().Count > 0)
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

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<ActionResult> Login(UserCredentialsDTO credentials) {
            if (_context.Users.ToList().Where(u => string.Equals(u.Email, credentials.Email, StringComparison.CurrentCultureIgnoreCase)).ToList().Count == 0) return BadRequest("Invalid Credentials");
            var foundUser = _context.Users.ToList().First(u => string.Equals(u.Email, credentials.Email, StringComparison.CurrentCultureIgnoreCase));
            if (!VerifyPasswordHash(credentials.Password, foundUser.PasswordHash, foundUser.PasswordSalt)) return BadRequest("Invalid Credentials");
            return Ok(new JsonObject() { new("token", GenerateJSONWebToken(foundUser)) }.ToJsonString());
        }
        
        private string GenerateJSONWebToken(User userInfo) {    
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));    
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha512Signature);    
    
            var token = new JwtSecurityToken(_configuration["Jwt:Issuer"],    
                _configuration["Jwt:Issuer"],    
                new [] {
                    new Claim(JwtRegisteredClaimNames.Sub, userInfo.Email),
                    new Claim(JwtRegisteredClaimNames.Email, userInfo.Email),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                    new Claim("admin", userInfo.Administrator ? true.ToString() : false.ToString())
                },    
                expires: DateTime.Now.AddMinutes(120),    
                signingCredentials: credentials);    
    
            return new JwtSecurityTokenHandler().WriteToken(token);    
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
