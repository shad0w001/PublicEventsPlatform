using Application.Abstractions.Messaging;

namespace Application.Groups.RemoveGroupMember;

public sealed record RemoveGroupMemberCommand(Guid GroupId, Guid UserId) : ICommand;
