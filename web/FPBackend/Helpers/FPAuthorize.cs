using Ardalis.Result;
using FPBackend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FPBackend.Helpers; 

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class FPAuthorize : Attribute, IAuthorizationFilter {
    public void OnAuthorization(AuthorizationFilterContext context) {
        var user = (Result<User>?) context.HttpContext.Items["User"];
        if (user is not { IsSuccess: true } || user.Value is null)
            context.Result = new JsonResult(new { message = "Unauthorized" }) {
                StatusCode = StatusCodes.Status401Unauthorized
            };
    }
}