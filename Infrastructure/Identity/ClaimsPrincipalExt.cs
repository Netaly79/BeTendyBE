using System.Security.Claims;

namespace BeTendyBE.Infrastructure.Identity;

public static class ClaimsPrincipalExt
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(sub)) throw new UnauthorizedAccessException("No user id in token.");
        return Guid.Parse(sub);
    }
}