using Domain.Groups;
using Domain.Participants;

namespace Domain.Users;

public class User : Participant
{
    public string ExternalSubjectId { get; set; } = null!;
    public string? Username { get; set; }
    public string Email { get; set; } = null!;
    public bool EmailVerified { get; set; }
    public ServiceRole ServiceRole { get; set; } = ServiceRole.User;
    public string? ProfilePictureUrl { get; set; }
    public string? Bio { get; set; }
    public DateTime LastActive { get; set; }
    public List<GroupMembership> GroupMemberships { get; set; } = [];

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

    public static User CreateFromExternalIdentity(
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

    public void SyncFromExternalIdentity(
        string email,
        bool emailVerified,
        string? profilePictureUrl,
        ServiceRole serviceRole)
    {
        Email = email;
        EmailVerified = emailVerified;

        if (profilePictureUrl is not null)
        {
            ProfilePictureUrl = profilePictureUrl;
        }

        ServiceRole = serviceRole;
        LastActive = DateTime.UtcNow;
    }
}
