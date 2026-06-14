using Application.Abstractions.Messaging;
using Application.Groups;

namespace Application.Groups.JoinGroup;

public sealed record JoinGroupCommand(Guid GroupId) : ICommand<GroupJoinMembershipResponse>;
