using Domain.Users;

namespace DomainTests.Users;

public class UserTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";

    [Fact]
    public void CreateDefaultUsernameFromEmail_Should_ReturnLocalPart_When_EmailIsValid()
    {
        // Arrange — valid email with local part
        const string email = "new@example.com";

        // Act
        var username = User.CreateDefaultUsernameFromEmail(email);

        // Assert
        Assert.Equal("new", username);
    }

    [Fact]
    public void CreateDefaultUsernameFromEmail_Should_ReturnUser_When_EmailHasNoLocalPart()
    {
        // Arrange — email with empty local part
        const string email = "@example.com";

        // Act
        var username = User.CreateDefaultUsernameFromEmail(email);

        // Assert
        Assert.Equal("user", username);
    }

    [Fact]
    public void CreateDefaultUsernameFromEmail_Should_ReturnUser_When_EmailHasNoAtSign()
    {
        // Arrange — email without @ separator
        const string email = "invalid";

        // Act
        var username = User.CreateDefaultUsernameFromEmail(email);

        // Assert
        Assert.Equal("user", username);
    }

    [Fact]
    public void CreateFromExternalIdentity_Should_SetUsernameFromEmail_When_UserIsProvisioned()
    {
        // Arrange — external identity with valid email
        const string email = "new@example.com";

        // Act
        var user = User.CreateFromExternalIdentity(
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
    public void CreateFromExternalIdentity_Should_LeaveBioNull_When_UserIsProvisioned()
    {
        // Arrange — external identity inputs
        const string email = "new@example.com";

        // Act
        var user = User.CreateFromExternalIdentity(
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
    public void CreateFromExternalIdentity_Should_UseAuth0Picture_When_PictureProvided()
    {
        // Arrange — Auth0 picture URL provided
        const string pictureUrl = "https://example.com/pic.jpg";

        // Act
        var user = User.CreateFromExternalIdentity(
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
    public void CreateFromExternalIdentity_Should_UseDefaultAvatar_When_PictureIsNull()
    {
        // Arrange — no Auth0 picture

        // Act
        var user = User.CreateFromExternalIdentity(
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
    public void SyncFromExternalIdentity_Should_PreserveUsernameAndBio_When_IdentityClaimsChange()
    {
        // Arrange — existing user with app-only fields set
        var user = User.CreateFromExternalIdentity(
            "auth0|subject",
            "old@example.com",
            emailVerified: false,
            profilePictureUrl: null,
            DefaultAvatarUrl,
            ServiceRole.User);
        user.Username = "customusername";
        user.Bio = "Custom bio";

        // Act
        user.SyncFromExternalIdentity(
            "updated@example.com",
            emailVerified: true,
            profilePictureUrl: null,
            ServiceRole.Admin);

        // Assert
        Assert.Equal("customusername", user.Username);
        Assert.Equal("Custom bio", user.Bio);
    }

    [Fact]
    public void SyncFromExternalIdentity_Should_UpdatePicture_When_Auth0PictureProvided()
    {
        // Arrange — existing user with default avatar
        var user = User.CreateFromExternalIdentity(
            "auth0|subject",
            "user@example.com",
            emailVerified: true,
            profilePictureUrl: null,
            DefaultAvatarUrl,
            ServiceRole.User);
        const string newPictureUrl = "https://example.com/new-pic.jpg";

        // Act
        user.SyncFromExternalIdentity(
            "user@example.com",
            emailVerified: true,
            newPictureUrl,
            ServiceRole.User);

        // Assert
        Assert.Equal(newPictureUrl, user.ProfilePictureUrl);
    }

    [Fact]
    public void SyncFromExternalIdentity_Should_NotClearPicture_When_Auth0PictureIsNull()
    {
        // Arrange — existing user with stored picture
        const string existingPictureUrl = "https://example.com/pic.jpg";
        var user = User.CreateFromExternalIdentity(
            "auth0|subject",
            "user@example.com",
            emailVerified: true,
            existingPictureUrl,
            DefaultAvatarUrl,
            ServiceRole.User);

        // Act
        user.SyncFromExternalIdentity(
            "user@example.com",
            emailVerified: true,
            profilePictureUrl: null,
            ServiceRole.User);

        // Assert
        Assert.Equal(existingPictureUrl, user.ProfilePictureUrl);
    }

    [Fact]
    public void SyncFromExternalIdentity_Should_UpdateEmailAndServiceRole_When_ClaimsChange()
    {
        // Arrange — existing user
        var user = User.CreateFromExternalIdentity(
            "auth0|subject",
            "old@example.com",
            emailVerified: false,
            profilePictureUrl: null,
            DefaultAvatarUrl,
            ServiceRole.User);

        // Act
        user.SyncFromExternalIdentity(
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
