using Application.Abstractions.Messaging;
using Application.Groups;

namespace Application.Groups.SubmitGroupJoinApplication;

public sealed record SubmitGroupJoinApplicationCommand(Guid GroupId)
    : ICommand<GroupJoinApplicationResponse>;
