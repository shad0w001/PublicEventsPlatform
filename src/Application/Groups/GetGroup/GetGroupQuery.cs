using Application.Abstractions.Messaging;

namespace Application.Groups.GetGroup;

public sealed record GetGroupQuery(Guid GroupId) : IQuery<PublicGroupResponse>;
