using Application.Abstractions.Messaging;
using Application.Groups.CreateGroup;
using Domain.Groups;

namespace Application.Groups.UpdateGroup;

public sealed record UpdateGroupCommand(
    Guid GroupId,
    string? Name = null,
    string? Description = null,
    string? ProfileImageUrl = null,
    GroupJoinPolicy? JoinPolicy = null) : ICommand<GroupResponse>;
