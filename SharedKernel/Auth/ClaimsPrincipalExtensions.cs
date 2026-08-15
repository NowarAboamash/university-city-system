using System.Security.Claims;

namespace SharedKernel.Auth
{
    public static class ClaimsPrincipalExtensions
    {
        public static bool TryGetUserId(this ClaimsPrincipal user, out string userId)
        {
            var sub = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
            userId = sub ?? string.Empty;
            return !string.IsNullOrWhiteSpace(sub);
        }

        public static string? GetEmail(this ClaimsPrincipal user) =>
            user.FindFirstValue("email") ?? user.FindFirstValue(ClaimTypes.Email);

        public static string? GetRole(this ClaimsPrincipal user) =>
            user.FindFirstValue("role") ?? user.FindFirstValue(ClaimTypes.Role);
    }
}
