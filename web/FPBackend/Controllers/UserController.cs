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
        var result = _userService.GetUserById(id);
        if (result.IsSuccess) return result.Value;
        return BadRequest();
    }

}