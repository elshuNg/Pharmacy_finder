using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using PharmacyFinder.API.Helpers;

namespace PharmacyFinder.API.Middleware;

public class AuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Challenged)
        {
            await ApiErrorWriter.WriteAsync(
                context,
                System.Net.HttpStatusCode.Unauthorized,
                ApiErrorCodes.Unauthorized,
                "Authentication is required.");
            return;
        }

        if (authorizeResult.Forbidden)
        {
            await ApiErrorWriter.WriteAsync(
                context,
                System.Net.HttpStatusCode.Forbidden,
                ApiErrorCodes.Forbidden,
                "You do not have permission to access this resource.");
            return;
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
