using Domain.Participants;

namespace Domain.Groups;

public class Group : Participant
{
    public string Name { get; internal set; } = string.Empty;
    public string Description { get; internal set; } = string.Empty;
    public string ProfileImageUrl { get; internal set; } = string.Empty;
    public GroupJoinPolicy JoinPolicy { get; internal set; } = GroupJoinPolicy.Open;
    public DateTime? DeletedAt { get; internal set; }
    public bool IsVerified { get; internal set; }
    public DateTime? VerifiedAt { get; internal set; }

    public bool IsDeleted => DeletedAt is not null;

    public List<GroupMembership> GroupMemberships { get; internal set; } = [];
    public List<GroupJoinApplication> JoinApplications { get; internal set; } = [];
    public List<GroupVerificationApplication> VerificationApplications { get; internal set; } = [];
}
