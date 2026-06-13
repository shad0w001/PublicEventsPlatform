using Application.Abstractions.Messaging;
using Domain.Groups;

namespace Application.Groups.ChangeMemberRole;

public sealed record ChangeMemberRoleCommand(
    Guid GroupId,
    Guid UserId,
    GroupMemberRole Role) : ICommand;
