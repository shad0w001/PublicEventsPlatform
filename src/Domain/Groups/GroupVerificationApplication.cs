using Domain.Users;
using SharedKernel;

namespace Domain.Groups;

public sealed class GroupVerificationApplication : Entity
{
    public Guid GroupId { get; internal set; }
    public Guid SubmittedByUserId { get; internal set; }
    public GroupVerificationApplicationStatus Status { get; internal set; }
    public DateTime SubmittedAt { get; internal set; }
    public DateTime? DecidedAt { get; internal set; }
    public Guid? DecidedByUserId { get; internal set; }

    public Group Group { get; internal set; } = null!;
    public User SubmittedByUser { get; internal set; } = null!;
}
