using Application.Abstractions.Authentication;
using Application.Subscriptions.DeleteSubscription;
using Application.Users;
using Application.Users.Services;
using Domain.Subscriptions;
using Domain.Users;
using Domain.Users.Services;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApplicationTests.Subscriptions;

public class DeleteSubscriptionCommandHandlerTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";

    [Fact]
    public async Task DeleteSubscriptionCommandHandler_Should_RemoveRow_When_SubscriptionBelongsToCaller()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|sub-delete", "sub-delete@example.com");
        await using var seedContext = CreateContext(databaseName);
        var user = await ProvisionUserAsync(seedContext, identity);
        var subscription = UserSubscription.Create(user.Id, SubscriptionKind.Online, null, null);
        seedContext.UserSubscriptions.Add(subscription);
        await seedContext.SaveChangesAsync(CancellationToken.None);

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(
            new DeleteSubscriptionCommand(subscription.Id),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(await context.UserSubscriptions.ToListAsync());
    }

    [Fact]
    public async Task DeleteSubscriptionCommandHandler_Should_ReturnNotFound_When_SubscriptionIdDoesNotExist()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|sub-delete-missing", "sub-delete-missing@example.com");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);
        var missingId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        // Act
        var result = await handler.Handle(new DeleteSubscriptionCommand(missingId), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Subscriptions.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task DeleteSubscriptionCommandHandler_Should_ReturnNotFound_When_SubscriptionBelongsToAnotherUser()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var ownerIdentity = CreateVerifiedIdentity("auth0|sub-delete-owner", "sub-delete-owner@example.com");
        var otherIdentity = CreateVerifiedIdentity("auth0|sub-delete-other", "sub-delete-other@example.com");
        await using var seedContext = CreateContext(databaseName);
        var owner = await ProvisionUserAsync(seedContext, ownerIdentity);
        await ProvisionUserAsync(seedContext, otherIdentity);
        var subscription = UserSubscription.Create(owner.Id, SubscriptionKind.City, "sofia", null);
        seedContext.UserSubscriptions.Add(subscription);
        await seedContext.SaveChangesAsync(CancellationToken.None);

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, otherIdentity);

        // Act
        var result = await handler.Handle(
            new DeleteSubscriptionCommand(subscription.Id),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Subscriptions.NotFound", result.Error.Code);
        Assert.Single(await context.UserSubscriptions.ToListAsync());
    }

    private static async Task<User> ProvisionUserAsync(
        ApplicationDbContext context,
        FakeUserIdentityAccessor identity)
    {
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        return (await currentUserService.GetOrProvisionAsync(CancellationToken.None)).Value;
    }

    private static FakeUserIdentityAccessor CreateVerifiedIdentity(string externalSubjectId, string email) =>
        new()
        {
            IsAuthenticated = true,
            ExternalSubjectId = externalSubjectId,
            Email = email,
            EmailVerified = true
        };

    private static DeleteSubscriptionCommandHandler CreateHandler(
        ApplicationDbContext context,
        FakeUserIdentityAccessor identity)
    {
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));

        return new DeleteSubscriptionCommandHandler(context, currentUserService, identity);
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
        public ServiceRole ServiceRole { get; init; } = ServiceRole.User;
        public string? ProfilePictureUrl { get; init; }
    }
}
