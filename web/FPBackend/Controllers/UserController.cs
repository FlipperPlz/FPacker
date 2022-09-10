using FPBackend.Context;
using FPBackend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPBackend.Controllers; 

[Route("api/user")]
[ApiController]
public class UserController : Controller {
    private readonly FPDataContext _context;
    private readonly IConfiguration _configuration;


    public UserController(FPDataContext context, IConfiguration config) {
        _context = context;
        _configuration = config;
    } 

    
    [Authorize, HttpGet("{id}")]
    public async Task<ActionResult<User>> GetUser(Guid id) {
        if (!HttpContext.User.HasClaim(c => c.Type == "admin" && c.Value == true.ToString())) 
            return Unauthorized();
        
        var user = await _context.Users.FindAsync(id);
        if (user is null) return NotFound("No user with this id was found");
        return Ok(user);

    }

}