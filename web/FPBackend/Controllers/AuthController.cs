using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FPBackend.Models;
using FPBackend.Models.DTO;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using FPBackend.Context;
using FPBackend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace FPBackend.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase {
        private readonly IUserService _userService;
        
        public AuthController(IUserService userService) {
            _userService = userService;
        } 
        
        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<ActionResult<User>> Register(UserRegistrationDTO credentials, CancellationToken token = default) {
            var result = await _userService.CreateUserAsync(credentials, token);
            if (result.IsSuccess) return Ok(result.Value);
            return BadRequest(result.Errors);
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public ActionResult<JwtResponseDTO> Login(UserCredentialsDTO credentials) {
            var result = _userService.Authenticate(credentials);
            if (result.IsSuccess) return Ok(result.Value);
            return BadRequest(result.Errors);
        }
    }
}
