using Domain.Users;
using SharedKernel;

namespace Domain.Groups;

public sealed class GroupJoinApplication : Entity
{
    public Guid GroupId { get; internal set; }
    public Guid UserId { get; internal set; }
    public GroupJoinApplicationStatus Status { get; internal set; }
    public DateTime SubmittedAt { get; internal set; }
    public DateTime? DecidedAt { get; internal set; }
    public Guid? DecidedByUserId { get; internal set; }

    public Group Group { get; internal set; } = null!;
    public User User { get; internal set; } = null!;
}
