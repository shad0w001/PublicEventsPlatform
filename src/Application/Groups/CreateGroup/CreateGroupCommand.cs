using Application.Abstractions.Messaging;
using Domain.Groups;

namespace Application.Groups.CreateGroup;

public sealed record CreateGroupCommand(
    string Name,
    string? Description,
    GroupJoinPolicy JoinPolicy,
    string? ProfileImageUrl) : ICommand<GroupResponse>;
