using System.Security.Claims;

namespace ASC.Utilities
{
    public static class ClaimsPrincipalExtensions
    {
        public static CurrentUser GetCurrentUserDetails(this ClaimsPrincipal principal)
        {
            if (principal == null || !principal.Claims.Any())
                return null;

            var name = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
            var email = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            var isActiveValue = principal.Claims.FirstOrDefault(c => c.Type == "IsActive")?.Value;

            bool isActive = false;
            bool.TryParse(isActiveValue, out isActive);

            return new CurrentUser
            {
                Name = name ?? email ?? "User",
                Email = email ?? string.Empty,
                Roles = principal.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray(),
                IsActive = isActive
            };
        }
    }
}