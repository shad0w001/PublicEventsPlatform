using Domain.Users;

namespace Application.Users.GetMe;

public sealed record UserResponse(
    Guid Id,
    string Email,
    bool EmailVerified,
    string? Username,
    string? Bio,
    string? ProfilePictureUrl,
    ServiceRole ServiceRole,
    DateTime CreatedAt,
    DateTime LastActive);
