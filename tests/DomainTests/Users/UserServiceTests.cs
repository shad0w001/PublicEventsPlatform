using Domain.Users;
using Domain.Users.Services;

namespace DomainTests.Users;

public class UserServiceTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";

    [Fact]
    public void UserService_Should_ReturnLocalPart_When_CreateDefaultUsernameFromEmailWithValidEmail()
    {
        // Arrange — valid email with local part
        const string email = "new@example.com";

        // Act
        var username = UserService.CreateDefaultUsernameFromEmail(email);

        // Assert
        Assert.Equal("new", username);
    }

    [Fact]
    public void UserService_Should_ReturnUser_When_CreateDefaultUsernameFromEmailWithNoLocalPart()
    {
        // Arrange — email with empty local part
        const string email = "@example.com";

        // Act
        var username = UserService.CreateDefaultUsernameFromEmail(email);

        // Assert
        Assert.Equal("user", username);
    }

    [Fact]
    public void UserService_Should_ReturnUser_When_CreateDefaultUsernameFromEmailWithNoAtSign()
    {
        // Arrange — email without @ separator
        const string email = "invalid";

        // Act
        var username = UserService.CreateDefaultUsernameFromEmail(email);

        // Assert
        Assert.Equal("user", username);
    }

    [Fact]
    public void UserService_Should_SetUsernameFromEmail_When_ProvisionFromExternalIdentity()
    {
        // Arrange — external identity with valid email
        const string email = "new@example.com";

        // Act
        var user = UserService.ProvisionFromExternalIdentity(
            "auth0|subject",
            email,
            emailVerified: true,
            profilePictureUrl: null,
            DefaultAvatarUrl,
            ServiceRole.User);

        // Assert
        Assert.Equal("new", user.Username);
    }

    [Fact]
    public void UserService_Should_LeaveBioNull_When_ProvisionFromExternalIdentity()
    {
        // Arrange — external identity inputs
        const string email = "new@example.com";

        // Act
        var user = UserService.ProvisionFromExternalIdentity(
            "auth0|subject",
            email,
            emailVerified: true,
            profilePictureUrl: null,
            DefaultAvatarUrl,
            ServiceRole.User);

        // Assert
        Assert.Null(user.Bio);
    }

    [Fact]
    public void UserService_Should_UseAuth0Picture_When_ProvisionFromExternalIdentityWithPicture()
    {
        // Arrange — Auth0 picture URL provided
        const string pictureUrl = "https://example.com/pic.jpg";

        // Act
        var user = UserService.ProvisionFromExternalIdentity(
            "auth0|subject",
            "user@example.com",
            emailVerified: true,
            pictureUrl,
            DefaultAvatarUrl,
            ServiceRole.User);

        // Assert
        Assert.Equal(pictureUrl, user.ProfilePictureUrl);
    }

    [Fact]
    public void UserService_Should_UseDefaultAvatar_When_ProvisionFromExternalIdentityWithNullPicture()
    {
        // Arrange — no Auth0 picture

        // Act
        var user = UserService.ProvisionFromExternalIdentity(
            "auth0|subject",
            "user@example.com",
            emailVerified: true,
            profilePictureUrl: null,
            DefaultAvatarUrl,
            ServiceRole.User);

        // Assert
        Assert.Equal(DefaultAvatarUrl, user.ProfilePictureUrl);
    }

    [Fact]
    public void UserService_Should_PreserveUsernameAndBio_When_SyncFromExternalIdentity()
    {
        // Arrange — existing user with app-only fields set
        var user = UserService.ProvisionFromExternalIdentity(
            "auth0|subject",
            "old@example.com",
            emailVerified: false,
            profilePictureUrl: null,
            DefaultAvatarUrl,
            ServiceRole.User);
        user.Username = "customusername";
        user.Bio = "Custom bio";

        // Act
        UserService.SyncFromExternalIdentity(
            user,
            "updated@example.com",
            emailVerified: true,
            profilePictureUrl: null,
            ServiceRole.Admin);

        // Assert
        Assert.Equal("customusername", user.Username);
        Assert.Equal("Custom bio", user.Bio);
    }

    [Fact]
    public void UserService_Should_UpdatePicture_When_SyncFromExternalIdentityWithAuth0Picture()
    {
        // Arrange — existing user with default avatar
        var user = UserService.ProvisionFromExternalIdentity(
            "auth0|subject",
            "user@example.com",
            emailVerified: true,
            profilePictureUrl: null,
            DefaultAvatarUrl,
            ServiceRole.User);
        const string newPictureUrl = "https://example.com/new-pic.jpg";

        // Act
        UserService.SyncFromExternalIdentity(
            user,
            "user@example.com",
            emailVerified: true,
            newPictureUrl,
            ServiceRole.User);

        // Assert
        Assert.Equal(newPictureUrl, user.ProfilePictureUrl);
    }

    [Fact]
    public void UserService_Should_NotClearPicture_When_SyncFromExternalIdentityWithNullPicture()
    {
        // Arrange — existing user with stored picture
        const string existingPictureUrl = "https://example.com/pic.jpg";
        var user = UserService.ProvisionFromExternalIdentity(
            "auth0|subject",
            "user@example.com",
            emailVerified: true,
            existingPictureUrl,
            DefaultAvatarUrl,
            ServiceRole.User);

        // Act
        UserService.SyncFromExternalIdentity(
            user,
            "user@example.com",
            emailVerified: true,
            profilePictureUrl: null,
            ServiceRole.User);

        // Assert
        Assert.Equal(existingPictureUrl, user.ProfilePictureUrl);
    }

    [Fact]
    public void UserService_Should_UpdateEmailAndServiceRole_When_SyncFromExternalIdentity()
    {
        // Arrange — existing user
        var user = UserService.ProvisionFromExternalIdentity(
            "auth0|subject",
            "old@example.com",
            emailVerified: false,
            profilePictureUrl: null,
            DefaultAvatarUrl,
            ServiceRole.User);

        // Act
        UserService.SyncFromExternalIdentity(
            user,
            "updated@example.com",
            emailVerified: true,
            profilePictureUrl: null,
            ServiceRole.Admin);

        // Assert
        Assert.Equal("updated@example.com", user.Email);
        Assert.True(user.EmailVerified);
        Assert.Equal(ServiceRole.Admin, user.ServiceRole);
    }
}
