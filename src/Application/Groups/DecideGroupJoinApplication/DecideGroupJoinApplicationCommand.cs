using Application.Abstractions.Messaging;

namespace Application.Groups.DecideGroupJoinApplication;

public sealed record DecideGroupJoinApplicationCommand(
    Guid GroupId,
    Guid ApplicationId,
    JoinApplicationDecision Decision) : ICommand;
