using Ardalis.Result;
using FPBackend.Context;
using FPBackend.Helpers;
using FPBackend.Models;
using FPBackend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPBackend.Controllers; 

[Route("api/user")]
[ApiController]
public class UserController : Controller {
     private readonly IUserService _userService;
    
    public UserController(IUserService userService) =>_userService = userService;
    
    [FPAdminAuthorize, HttpGet("{id:guid}")]
    public ActionResult<User> GetUser(Guid id) {
        try {
            if (HttpContext.User.HasClaim(c => c.Type == "id")) {
                Result<User> user = _userService.GetUserById(Guid.Parse(HttpContext.User.Claims.FirstOrDefault(c => c.Type == "id")!.Value)) ?? Result<User>.Error();
                if (!user.IsSuccess) return BadRequest(user.Errors);
                if (user.Value.Administrator) return Ok(user.Value);
            }
            return Unauthorized();
        }
        catch (Exception e) {
            return BadRequest(e);
        }
        
    }

}