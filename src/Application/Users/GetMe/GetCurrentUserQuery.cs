using Application.Abstractions.Messaging;

namespace Application.Users.GetMe;

public sealed record GetCurrentUserQuery : IQuery<UserResponse>;
