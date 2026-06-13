using Application.Abstractions.Authentication;
using Domain.Users;
using SharedKernel;

namespace ApplicationTests.Authentication;

public class VerifiedUserGateTests
{
    [Fact]
    public void VerifiedUserGate_Should_Succeed_When_UserIsVerified()
    {
        // Arrange
        var identity = new FakeUserIdentityAccessor
        {
            IsAuthenticated = true,
            EmailVerified = true
        };

        // Act
        var result = VerifiedUserGate.EnsureVerified(identity);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void VerifiedUserGate_Should_ReturnForbidden_When_EmailNotVerified()
    {
        // Arrange
        var identity = new FakeUserIdentityAccessor
        {
            IsAuthenticated = true,
            EmailVerified = false
        };

        // Act
        var result = VerifiedUserGate.EnsureVerified(identity);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
        Assert.Equal("Users.EmailNotVerified", result.Error.Code);
    }

    [Fact]
    public void VerifiedUserGate_Should_ReturnUnauthorized_When_NotAuthenticated()
    {
        // Arrange
        var identity = new FakeUserIdentityAccessor { IsAuthenticated = false };

        // Act
        var result = VerifiedUserGate.EnsureVerified(identity);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Faulure, result.Error.Type);
        Assert.Equal("Users.Unauthorized", result.Error.Code);
    }

    private sealed class FakeUserIdentityAccessor : IUserIdentityAccessor
    {
        public bool IsAuthenticated { get; init; }
        public string ExternalSubjectId { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public bool EmailVerified { get; init; }
        public ServiceRole ServiceRole { get; init; }
        public string? ProfilePictureUrl { get; init; }
    }
}
