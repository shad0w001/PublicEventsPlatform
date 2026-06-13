using System.Security.Claims;
using System.Text.Json;

namespace Infrastructure.Authentication;

public static class ClaimsPrincipalExtensions
{
    public static string? GetSubjectId(this ClaimsPrincipal principal) =>
        principal.FindFirstValue("sub");

    public static string? GetEmail(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(Auth0ClaimTypes.Email);

    public static bool GetEmailVerified(this ClaimsPrincipal principal) =>
        bool.TryParse(principal.FindFirstValue(Auth0ClaimTypes.EmailVerified), out var verified) && verified;

    public static string? GetPictureUrl(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(Auth0ClaimTypes.Picture);

    public static IEnumerable<string> GetRoles(this ClaimsPrincipal principal)
    {
        var claims = principal.FindAll(Auth0ClaimTypes.Roles).ToList();
        if (claims.Count == 0)
        {
            return [];
        }

        var roles = new List<string>();

        foreach (var claim in claims)
        {
            var value = claim.Value.Trim();

            if (value.StartsWith('['))
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<string[]>(value);
                    if (parsed is not null)
                    {
                        roles.AddRange(parsed);
                    }
                }
                catch (JsonException)
                {
                    // Ignore malformed JSON role payloads.
                }
            }
            else
            {
                roles.Add(value);
            }
        }

        return roles;
    }
}
