using Domain.Users;

namespace Domain.Groups;

public class GroupMembership
{
    public Guid GroupId { get; set; }
    public Guid UserId { get; set; }
    public DateTime JoinedAt { get; set; }
    public GroupMemberRole Role { get; set; }

    public Group Group { get; set; } = null!;
    public User User { get; set; } = null!;

    public static GroupMembership Create(Guid groupId, Guid userId, GroupMemberRole role) =>
        new()
        {
            GroupId = groupId,
            UserId = userId,
            Role = role,
            JoinedAt = DateTime.UtcNow
        };
}
