using FPBackend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FPBackend.Helpers; 

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class FPAdminAuthorize : Attribute, IAuthorizationFilter {
    public void OnAuthorization(AuthorizationFilterContext context) {
        var user = (User?) context.HttpContext.Items["User"];
        if (user is not { Administrator: true }) context.Result = new JsonResult(new { message = "Unauthorized" }) { StatusCode = StatusCodes.Status401Unauthorized };
    }
}