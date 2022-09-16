using Ardalis.Result;
using FPBackend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FPBackend.Helpers; 

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class FPAdminAuthorize : Attribute, IAuthorizationFilter {
    public void OnAuthorization(AuthorizationFilterContext context) {
        var userResult = (Result<User>?) context.HttpContext.Items["User"];
        if ( userResult is null || !userResult.IsSuccess || userResult.Value is not { Administrator: true })
            context.Result = new JsonResult(new { message = "Unauthorized" }) {
                StatusCode = StatusCodes.Status401Unauthorized
            };
    }
}