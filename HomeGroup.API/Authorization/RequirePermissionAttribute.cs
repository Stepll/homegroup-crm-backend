using HomeGroup.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HomeGroup.API.Authorization;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class RequirePermissionAttribute(string permission) : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (!user.Identity?.IsAuthenticated ?? true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var permissions = user.FindAll(JwtService.PermissionClaimType)
            .Select(c => c.Value)
            .ToHashSet();

        if (permissions.Contains("*") || permissions.Contains(permission))
            return;

        context.Result = new ObjectResult(new { message = "Недостатньо прав доступу" })
        {
            StatusCode = 403
        };
    }
}
