using Application.Abstractions.Messaging;
using Application.Groups;

namespace Application.Groups.GetGroup;

public sealed record GetGroupQuery(Guid GroupId) : IQuery<GroupPageResponse>;
