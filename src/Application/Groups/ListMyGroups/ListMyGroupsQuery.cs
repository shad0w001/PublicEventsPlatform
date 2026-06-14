using Application.Abstractions.Messaging;

namespace Application.Groups.ListMyGroups;

public sealed record ListMyGroupsQuery : IQuery<IReadOnlyList<MyGroupMembershipResponse>>;
