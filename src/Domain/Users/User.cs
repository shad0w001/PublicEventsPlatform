using Domain.Groups;
using Domain.Participants;

namespace Domain.Users;

public class User : Participant
{
    public string ExternalSubjectId { get; internal set; } = null!;
    public string? Username { get; set; }
    public string Email { get; internal set; } = null!;
    public bool EmailVerified { get; internal set; }
    public ServiceRole ServiceRole { get; internal set; } = ServiceRole.User;
    public string? ProfilePictureUrl { get; internal set; }
    public string? Bio { get; set; }
    public DateTime LastActive { get; internal set; }
    public List<GroupMembership> GroupMemberships { get; set; } = [];
}
