using Application.Abstractions.Messaging;

namespace Application.Groups.DeleteGroup;

public sealed record DeleteGroupCommand(Guid GroupId) : ICommand;
