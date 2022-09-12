using System.IdentityModel.Tokens.Jwt;
using System.Text;
using FPBackend.Services;
using Microsoft.IdentityModel.Tokens;

namespace FPBackend.Helpers; 

public class JwtMiddleware {
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;

    public JwtMiddleware(RequestDelegate next, IConfiguration configuration) {
        _next = next;
        _configuration = configuration;
    }
    
    public async Task Invoke(HttpContext context, IUserService userService) {
        var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();

        if (token != null) AttachUserToContext(context, userService, token);

        await _next(context);
    }
    
    private void AttachUserToContext(HttpContext context, IUserService userService, string token) {
        try {
            var tokenHandler = new JwtSecurityTokenHandler();
            tokenHandler.ValidateToken(token, new TokenValidationParameters {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _configuration["JWT:Issuer"],
                ValidAudience = _configuration["JWT:Issuer"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:Key"])),
                ClockSkew = TimeSpan.Zero
            }, out var validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;
            var userId = Guid.Parse(jwtToken.Claims.First(x => x.Type == "id").Value);
            
            context.Items["User"] = userService.GetUserById(userId);
        }
        catch {
            //
        }
    }
}