using Application.Abstractions.Messaging;

namespace Application.Groups.CancelGroupJoinApplication;

public sealed record CancelGroupJoinApplicationCommand(
    Guid GroupId,
    Guid ApplicationId) : ICommand;
