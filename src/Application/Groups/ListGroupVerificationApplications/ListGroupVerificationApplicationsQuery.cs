using Application.Abstractions.Messaging;

namespace Application.Groups.ListGroupVerificationApplications;

public sealed record ListGroupVerificationApplicationsQuery : IQuery<IReadOnlyList<GroupVerificationApplicationResponse>>;
