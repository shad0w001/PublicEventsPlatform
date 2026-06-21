using Application.Abstractions.Messaging;

namespace Application.Groups.GetGroupVerificationApplication;

public sealed record GetGroupVerificationApplicationQuery(Guid ApplicationId)
    : IQuery<GroupVerificationApplicationDetailResponse>;
