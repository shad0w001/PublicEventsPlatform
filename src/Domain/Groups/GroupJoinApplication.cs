using Domain.Groups.Events;
using Domain.Participants;
using Domain.Users;
using SharedKernel;

namespace Domain.Groups;

public sealed class GroupJoinApplication : Entity
{
    public Guid GroupId { get; private set; }
    public Guid UserId { get; private set; }
    public GroupJoinApplicationStatus Status { get; private set; }
    public DateTime SubmittedAt { get; private set; }
    public DateTime? DecidedAt { get; private set; }
    public Guid? DecidedByUserId { get; private set; }

    public Group Group { get; private set; } = null!;
    public User User { get; private set; } = null!;

    private GroupJoinApplication()
    {
    }

    internal static GroupJoinApplication CreatePending(Guid groupId, Guid userId, DateTime submittedAt) =>
        new()
        {
            GroupId = groupId,
            UserId = userId,
            Status = GroupJoinApplicationStatus.Pending,
            SubmittedAt = submittedAt
        };

    internal Result Approve(DateTime decidedAt, Guid decidedByUserId)
    {
        if (Status != GroupJoinApplicationStatus.Pending)
        {
            return Result.Failure(GroupJoinApplicationErrors.NotPending);
        }

        Status = GroupJoinApplicationStatus.Approved;
        DecidedAt = decidedAt;
        DecidedByUserId = decidedByUserId;

        return Result.Success();
    }

    internal Result Reject(DateTime decidedAt, Guid decidedByUserId)
    {
        if (Status != GroupJoinApplicationStatus.Pending)
        {
            return Result.Failure(GroupJoinApplicationErrors.NotPending);
        }

        Status = GroupJoinApplicationStatus.Rejected;
        DecidedAt = decidedAt;
        DecidedByUserId = decidedByUserId;

        return Result.Success();
    }
}
