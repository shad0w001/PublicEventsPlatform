using Application.Abstractions.Messaging;
using Application.Groups;

namespace Application.Groups.ListGroupJoinApplications;

public sealed record ListGroupJoinApplicationsQuery(Guid GroupId)
    : IQuery<IReadOnlyList<GroupJoinApplicationResponse>>;
