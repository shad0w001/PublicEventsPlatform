using Application.Abstractions.Authentication;
using Application.Subscriptions.CreateSubscription;
using Application.Subscriptions.ListMySubscriptions;
using Application.Users;
using Application.Users.Services;
using Domain.Events;
using Domain.Subscriptions;
using Domain.Users;
using Domain.Users.Services;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApplicationTests.Subscriptions;

public class ListMySubscriptionsQueryHandlerTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";

    [Fact]
    public async Task ListMySubscriptionsQueryHandler_Should_ReturnOnlyCallerRows_When_OtherUserHasSubscriptions()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var callerIdentity = CreateVerifiedIdentity("auth0|sub-list-caller", "sub-list-caller@example.com");
        var otherIdentity = CreateVerifiedIdentity("auth0|sub-list-other", "sub-list-other@example.com");
        await using var seedContext = CreateContext(databaseName);
        var caller = await ProvisionUserAsync(seedContext, callerIdentity);
        var other = await ProvisionUserAsync(seedContext, otherIdentity);
        seedContext.UserSubscriptions.Add(UserSubscription.Create(caller.Id, SubscriptionKind.City, "sofia", null));
        seedContext.UserSubscriptions.Add(UserSubscription.Create(other.Id, SubscriptionKind.Online, null, null));
        await seedContext.SaveChangesAsync(CancellationToken.None);

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, callerIdentity);

        // Act
        var result = await handler.Handle(new ListMySubscriptionsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal(SubscriptionKind.City, result.Value[0].Kind);
        Assert.Equal("sofia", result.Value[0].City);
    }

    [Fact]
    public async Task ListMySubscriptionsQueryHandler_Should_IncludeCategoryName_When_CategorySubscriptionExists()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|sub-list-cat", "sub-list-cat@example.com");
        await using var seedContext = CreateContext(databaseName);
        var categoryId = SeedCategory(seedContext, "Music");
        var user = await ProvisionUserAsync(seedContext, identity);
        seedContext.UserSubscriptions.Add(
            UserSubscription.Create(user.Id, SubscriptionKind.Category, null, categoryId));
        await seedContext.SaveChangesAsync(CancellationToken.None);

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new ListMySubscriptionsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal(categoryId, result.Value[0].CategoryId);
        Assert.Equal("Music", result.Value[0].CategoryName);
    }

    [Fact]
    public async Task ListMySubscriptionsQueryHandler_Should_SortByCreatedAtDescending_When_MultipleSubscriptionsExist()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|sub-list-sort", "sub-list-sort@example.com");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);
        var createHandler = new CreateSubscriptionCommandHandler(
            context,
            new CurrentUserService(
                context,
                identity,
                Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl })),
            identity);

        await createHandler.Handle(
            new CreateSubscriptionCommand(SubscriptionKind.City, "Plovdiv", null),
            CancellationToken.None);
        await createHandler.Handle(
            new CreateSubscriptionCommand(SubscriptionKind.Online, null, null),
            CancellationToken.None);

        // Act
        var result = await handler.Handle(new ListMySubscriptionsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal(SubscriptionKind.Online, result.Value[0].Kind);
        Assert.Equal(SubscriptionKind.City, result.Value[1].Kind);
        Assert.True(result.Value[0].CreatedAt >= result.Value[1].CreatedAt);
    }

    private static Guid SeedCategory(ApplicationDbContext context, string name)
    {
        var category = new EventCategory { Name = name };
        context.EventCategories.Add(category);
        context.SaveChanges();
        return category.Id;
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

    private static ListMySubscriptionsQueryHandler CreateHandler(
        ApplicationDbContext context,
        FakeUserIdentityAccessor identity)
    {
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));

        return new ListMySubscriptionsQueryHandler(context, currentUserService, identity);
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
