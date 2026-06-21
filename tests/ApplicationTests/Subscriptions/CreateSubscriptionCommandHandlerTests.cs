using Application.Abstractions.Authentication;
using Application.Subscriptions.CreateSubscription;
using Application.Users;
using Application.Users.Services;
using Domain.Events;
using Domain.Subscriptions;
using Domain.Subscriptions.Services;
using Domain.Users;
using Domain.Users.Services;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApplicationTests.Subscriptions;

public class CreateSubscriptionCommandHandlerTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";

    [Fact]
    public async Task CreateSubscriptionCommandHandler_Should_CreateCitySubscriptionWithNormalizedCity_When_CityIsValid()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|sub-city", "sub-city@example.com");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(
            new CreateSubscriptionCommand(SubscriptionKind.City, "Sofia", null),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(SubscriptionKind.City, result.Value.Kind);
        Assert.Equal("sofia", result.Value.City);
        Assert.Null(result.Value.CategoryId);
        Assert.Null(result.Value.CategoryName);

        var persisted = await context.UserSubscriptions.SingleAsync();
        Assert.Equal("sofia", persisted.City);
    }

    [Fact]
    public async Task CreateSubscriptionCommandHandler_Should_CreateCategorySubscriptionWithName_When_CategoryExists()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|sub-category", "sub-category@example.com");
        await using var context = CreateContext(databaseName);
        var categoryId = SeedCategory(context, "Music");
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(
            new CreateSubscriptionCommand(SubscriptionKind.Category, null, categoryId),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(SubscriptionKind.Category, result.Value.Kind);
        Assert.Equal(categoryId, result.Value.CategoryId);
        Assert.Equal("Music", result.Value.CategoryName);
        Assert.Null(result.Value.City);
    }

    [Fact]
    public async Task CreateSubscriptionCommandHandler_Should_CreateOnlineSubscription_When_NoExtraFields()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|sub-online", "sub-online@example.com");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(
            new CreateSubscriptionCommand(SubscriptionKind.Online, null, null),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(SubscriptionKind.Online, result.Value.Kind);
        Assert.Null(result.Value.City);
        Assert.Null(result.Value.CategoryId);
    }

    [Fact]
    public async Task CreateSubscriptionCommandHandler_Should_ReturnDuplicate_When_SameCitySubscribedTwice()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|sub-dup", "sub-dup@example.com");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);
        var command = new CreateSubscriptionCommand(SubscriptionKind.City, "Sofia", null);

        // Act
        var first = await handler.Handle(command, CancellationToken.None);
        var second = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(first.IsSuccess);
        Assert.True(second.IsFailure);
        Assert.Equal("Subscriptions.Duplicate", second.Error.Code);
    }

    [Fact]
    public async Task CreateSubscriptionCommandHandler_Should_ReturnMaxLimitReached_When_UserHasThirtySubscriptions()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|sub-max", "sub-max@example.com");
        await using var context = CreateContext(databaseName);
        var user = await ProvisionUserAsync(context, identity);
        var existing = new List<UserSubscription>();

        for (var i = 0; i < SubscriptionConstants.MaxSubscriptionsPerUser; i++)
        {
            var createResult = SubscriptionService.Create(
                user.Id,
                SubscriptionKind.City,
                $"City{i}",
                null,
                categoryExists: false,
                existing);
            existing.Add(createResult.Value);
            context.UserSubscriptions.Add(createResult.Value);
        }

        await context.SaveChangesAsync(CancellationToken.None);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(
            new CreateSubscriptionCommand(SubscriptionKind.Online, null, null),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Subscriptions.MaxLimitReached", result.Error.Code);
    }

    [Fact]
    public async Task CreateSubscriptionCommandHandler_Should_ReturnCategoryNotFound_When_CategoryDoesNotExist()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|sub-no-cat", "sub-no-cat@example.com");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);
        var missingCategoryId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        // Act
        var result = await handler.Handle(
            new CreateSubscriptionCommand(SubscriptionKind.Category, null, missingCategoryId),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Subscriptions.CategoryNotFound", result.Error.Code);
    }

    [Fact]
    public async Task CreateSubscriptionCommandHandler_Should_ReturnInvalidKindPayload_When_OnlineHasCity()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|sub-invalid", "sub-invalid@example.com");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(
            new CreateSubscriptionCommand(SubscriptionKind.Online, "Sofia", null),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Subscriptions.InvalidKindPayload", result.Error.Code);
    }

    [Fact]
    public async Task CreateSubscriptionCommandHandler_Should_ReturnEmailNotVerified_When_CallerIsUnverified()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = new FakeUserIdentityAccessor
        {
            IsAuthenticated = true,
            ExternalSubjectId = "auth0|sub-unverified",
            Email = "sub-unverified@example.com",
            EmailVerified = false
        };
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(
            new CreateSubscriptionCommand(SubscriptionKind.Online, null, null),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Users.EmailNotVerified", result.Error.Code);
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

    private static CreateSubscriptionCommandHandler CreateHandler(
        ApplicationDbContext context,
        FakeUserIdentityAccessor identity)
    {
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));

        return new CreateSubscriptionCommandHandler(context, currentUserService, identity);
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
