using Application.Abstractions.Authentication;
using Domain.Users;
using SharedKernel;

namespace ApplicationTests.Authentication;

public class AdminGateTests
{
    [Fact]
    public void AdminGate_Should_Succeed_When_UserIsPlatformAdmin()
    {
        // Arrange
        var identity = new FakeUserIdentityAccessor
        {
            IsAuthenticated = true,
            ServiceRole = ServiceRole.Admin
        };

        // Act
        var result = AdminGate.EnsureAdmin(identity);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void AdminGate_Should_ReturnForbidden_When_UserIsNotAdmin()
    {
        // Arrange
        var identity = new FakeUserIdentityAccessor
        {
            IsAuthenticated = true,
            ServiceRole = ServiceRole.User
        };

        // Act
        var result = AdminGate.EnsureAdmin(identity);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
        Assert.Equal("Users.InsufficientAdminPermissions", result.Error.Code);
    }

    [Fact]
    public void AdminGate_Should_ReturnUnauthorized_When_NotAuthenticated()
    {
        // Arrange
        var identity = new FakeUserIdentityAccessor { IsAuthenticated = false };

        // Act
        var result = AdminGate.EnsureAdmin(identity);

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
