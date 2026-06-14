using Application.Abstractions.Messaging;

namespace Application.Groups.TransferOwnership;

public sealed record TransferOwnershipCommand(Guid GroupId, Guid NewOwnerUserId) : ICommand;
