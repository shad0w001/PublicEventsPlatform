using Application.Abstractions.Authentication;
using Application.Users;
using Domain.Users;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace ApplicationTests.Users;

public class CurrentUserServiceTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";

    [Fact]
    public async Task CurrentUserService_Should_ProvisionUser_When_ExternalSubjectNotFound()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = new FakeUserIdentityAccessor
        {
            IsAuthenticated = true,
            ExternalSubjectId = "auth0|provision-subject",
            Email = "provision@example.com",
            EmailVerified = true,
            ServiceRole = ServiceRole.User
        };

        await using var context = CreateContext(databaseName);
        var service = CreateService(context, identity);

        // Act
        var result = await service.GetOrProvisionAsync(CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("provision@example.com", result.Value.Email);

        await using var verifyContext = CreateContext(databaseName);
        var persistedUser = await verifyContext.Users.SingleAsync();
        Assert.Equal("auth0|provision-subject", persistedUser.ExternalSubjectId);
    }

    [Fact]
    public async Task CurrentUserService_Should_SyncExistingUser_When_ExternalSubjectExists()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        const string externalSubjectId = "auth0|sync-subject";

        await using (var seedContext = CreateContext(databaseName))
        {
            var existingUser = User.CreateFromExternalIdentity(
                externalSubjectId,
                "old@example.com",
                emailVerified: false,
                profilePictureUrl: null,
                DefaultAvatarUrl,
                ServiceRole.User);
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
        var service = CreateService(context, identity);

        // Act
        var result = await service.GetOrProvisionAsync(CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value.EmailVerified);

        await using var verifyContext = CreateContext(databaseName);
        var persistedUser = await verifyContext.Users.SingleAsync();
        Assert.Equal("updated@example.com", persistedUser.Email);
        Assert.True(persistedUser.EmailVerified);
    }

    [Fact]
    public async Task CurrentUserService_Should_ReturnUnauthorized_When_NotAuthenticated()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = new FakeUserIdentityAccessor { IsAuthenticated = false };

        await using var context = CreateContext(databaseName);
        var service = CreateService(context, identity);

        // Act
        var result = await service.GetOrProvisionAsync(CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Faulure, result.Error.Type);
        Assert.Equal("Users.Unauthorized", result.Error.Code);
    }

    private static CurrentUserService CreateService(
        ApplicationDbContext context,
        IUserIdentityAccessor identity) =>
        new(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));

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
