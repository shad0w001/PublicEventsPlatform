using Application.Abstractions.Messaging;

namespace Application.Groups.DecideGroupVerificationApplication;

public sealed record DecideGroupVerificationApplicationCommand(
    Guid ApplicationId,
    VerificationApplicationDecision Decision) : ICommand;
