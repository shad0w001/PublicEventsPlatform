namespace Infrastructure.Authentication;

public static class Auth0ClaimTypes
{
    public const string Namespace = "https://api.public-events-platform";
    public const string Email = $"{Namespace}/email";
    public const string EmailVerified = $"{Namespace}/email_verified";
    public const string Roles = $"{Namespace}/roles";
}
