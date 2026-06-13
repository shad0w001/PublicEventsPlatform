using SharedKernel;

namespace Domain.Users.Services;

public static class UserService
{
    public static string CreateDefaultUsernameFromEmail(string email)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex <= 0)
        {
            return "user";
        }

        var localPart = email[..atIndex].Trim();
        return string.IsNullOrEmpty(localPart) ? "user" : localPart;
    }

    public static User ProvisionFromExternalIdentity(
        string externalSubjectId,
        string email,
        bool emailVerified,
        string? profilePictureUrl,
        string defaultAvatarUrl,
        ServiceRole serviceRole)
    {
        return new User
        {
            ExternalSubjectId = externalSubjectId,
            Email = email,
            EmailVerified = emailVerified,
            Username = CreateDefaultUsernameFromEmail(email),
            ProfilePictureUrl = profilePictureUrl ?? defaultAvatarUrl,
            ServiceRole = serviceRole,
            LastActive = DateTime.UtcNow
        };
    }

    public static void SyncFromExternalIdentity(
        User user,
        string email,
        bool emailVerified,
        string? profilePictureUrl,
        ServiceRole serviceRole)
    {
        user.Email = email;
        user.EmailVerified = emailVerified;

        if (profilePictureUrl is not null)
        {
            user.ProfilePictureUrl = profilePictureUrl;
        }

        user.ServiceRole = serviceRole;
        user.LastActive = DateTime.UtcNow;
    }
}
