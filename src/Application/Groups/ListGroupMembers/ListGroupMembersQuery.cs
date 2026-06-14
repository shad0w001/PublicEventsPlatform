using Application.Abstractions.Messaging;

namespace Application.Groups.ListGroupMembers;

public sealed record ListGroupMembersQuery(Guid GroupId) : IQuery<IReadOnlyList<GroupMemberResponse>>;
