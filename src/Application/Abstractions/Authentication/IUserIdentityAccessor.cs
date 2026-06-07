using Domain.Users;

namespace Application.Abstractions.Authentication;

public interface IUserIdentityAccessor
{
    bool IsAuthenticated { get; }
    string ExternalSubjectId { get; }
    string Email { get; }
    bool EmailVerified { get; }
    ServiceRole ServiceRole { get; }
    string? ProfilePictureUrl { get; }
}
