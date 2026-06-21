using Application.Abstractions.Messaging;

namespace Application.Groups.SubmitGroupVerificationApplication;

public sealed record SubmitGroupVerificationApplicationCommand(Guid GroupId)
    : ICommand<GroupVerificationApplicationResponse>;
