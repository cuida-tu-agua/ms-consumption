using System.Security.Claims;

namespace SyWater.Consumption.Api.Security;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The owner of every request is the user in the token ("sub" = security.users.id).
    /// It never comes from the URL or the body, so nobody can act on behalf of someone else.
    /// </summary>
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id)
            ? id
            : throw new UnauthorizedAccessException("The token has no valid 'sub' claim.");
    }
}
