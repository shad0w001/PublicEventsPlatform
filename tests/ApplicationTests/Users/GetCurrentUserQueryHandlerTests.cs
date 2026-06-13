using Application.Abstractions.Authentication;
using Application.Users.GetMe;
using Domain.Users;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace ApplicationTests.Users;

public class GetCurrentUserQueryHandlerTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";

    [Fact]
    public async Task GetCurrentUserQueryHandler_Should_CreateUser_When_ExternalSubjectNotFound()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = new FakeUserIdentityAccessor
        {
            IsAuthenticated = true,
            ExternalSubjectId = "auth0|new-user-subject",
            Email = "new@example.com",
            EmailVerified = true,
            ServiceRole = ServiceRole.User
        };

        await using var context = CreateContext(databaseName);
        var handler = new GetCurrentUserQueryHandler(context, identity);

        // Act
        var result = await handler.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
        Assert.Equal("new@example.com", result.Value.Email);
        Assert.True(result.Value.EmailVerified);
        Assert.Equal(ServiceRole.User, result.Value.ServiceRole);
        Assert.Equal("new", result.Value.Username);
        Assert.Equal(DefaultAvatarUrl, result.Value.ProfilePictureUrl);

        await using var verifyContext = CreateContext(databaseName);
        var persistedUser = await verifyContext.Users.SingleAsync();
        Assert.Equal("auth0|new-user-subject", persistedUser.ExternalSubjectId);
        Assert.Equal("new@example.com", persistedUser.Email);
        Assert.Equal("new", persistedUser.Username);
        Assert.Equal(DefaultAvatarUrl, persistedUser.ProfilePictureUrl);
    }

    [Fact]
    public async Task GetCurrentUserQueryHandler_Should_SyncExistingUser_When_ExternalSubjectExists()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        const string externalSubjectId = "auth0|existing-subject";

        await using (var seedContext = CreateContext(databaseName))
        {
            var existingUser = User.CreateFromExternalIdentity(
                externalSubjectId,
                "old@example.com",
                emailVerified: false,
                profilePictureUrl: null,
                DefaultAvatarUrl,
                ServiceRole.User);
            existingUser.Username = "existinguser";
            seedContext.Users.Add(existingUser);
            await seedContext.SaveChangesAsync();
        }

        var identity = new FakeUserIdentityAccessor
        {
            IsAuthenticated = true,
            ExternalSubjectId = externalSubjectId,
            Email = "updated@example.com",
            EmailVerified = true,
            ServiceRole = ServiceRole.User
        };

        await using var context = CreateContext(databaseName);
        var handler = new GetCurrentUserQueryHandler(context, identity);

        // Act
        var result = await handler.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("updated@example.com", result.Value.Email);
        Assert.True(result.Value.EmailVerified);
        Assert.Equal("existinguser", result.Value.Username);

        await using var verifyContext = CreateContext(databaseName);
        var persistedUser = await verifyContext.Users.SingleAsync();
        Assert.Equal("updated@example.com", persistedUser.Email);
        Assert.True(persistedUser.EmailVerified);
        Assert.Equal("existinguser", persistedUser.Username);
    }

    [Fact]
    public async Task GetCurrentUserQueryHandler_Should_MapAdminServiceRole_When_AdminRoleInClaims()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = new FakeUserIdentityAccessor
        {
            IsAuthenticated = true,
            ExternalSubjectId = "auth0|admin-subject",
            Email = "admin@example.com",
            EmailVerified = true,
            ServiceRole = ServiceRole.Admin
        };

        await using var context = CreateContext(databaseName);
        var handler = new GetCurrentUserQueryHandler(context, identity);

        // Act
        var result = await handler.Handle(new GetCurrentUserQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ServiceRole.Admin, result.Value.ServiceRole);

        await using var verifyContext = CreateContext(databaseName);
        var persistedUser = await verifyContext.Users.SingleAsync();
        Assert.Equal(ServiceRole.Admin, persistedUser.ServiceRole);
    }

    private static ApplicationDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new ApplicationDbContext(options);
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
