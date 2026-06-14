using Application.Abstractions.Messaging;

namespace Application.Users.ListMyJoinApplications;

public sealed record ListMyJoinApplicationsQuery : IQuery<IReadOnlyList<MyJoinApplicationResponse>>;
