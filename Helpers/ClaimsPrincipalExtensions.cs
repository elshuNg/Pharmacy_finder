using System.Security.Claims;

namespace PharmacyFinder.API.Helpers;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var userId = principal.FindFirstValue("userId");
        if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out var id))
            throw ApiException.Unauthorized(ApiErrorCodes.AuthInvalidToken, "Invalid or missing user token.");

        return id;
    }
}
