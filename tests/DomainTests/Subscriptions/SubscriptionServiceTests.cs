using Domain.Subscriptions;
using Domain.Subscriptions.Services;

namespace DomainTests.Subscriptions;

public class SubscriptionServiceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CategoryId = Guid.Parse("e0000001-0001-4000-8000-000000000001");

    [Fact]
    public void SubscriptionService_Should_CreateCitySubscription_When_CityIsValid()
    {
        // Arrange
        var existing = Array.Empty<UserSubscription>();

        // Act
        var result = SubscriptionService.Create(
            UserId,
            SubscriptionKind.City,
            "Sofia",
            categoryId: null,
            categoryExists: false,
            existing);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(SubscriptionKind.City, result.Value.Kind);
        Assert.Equal("sofia", result.Value.City);
        Assert.Null(result.Value.CategoryId);
    }

    [Fact]
    public void SubscriptionService_Should_CreateCategorySubscription_When_CategoryExists()
    {
        // Arrange
        var existing = Array.Empty<UserSubscription>();

        // Act
        var result = SubscriptionService.Create(
            UserId,
            SubscriptionKind.Category,
            city: null,
            CategoryId,
            categoryExists: true,
            existing);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(SubscriptionKind.Category, result.Value.Kind);
        Assert.Equal(CategoryId, result.Value.CategoryId);
        Assert.Null(result.Value.City);
    }

    [Fact]
    public void SubscriptionService_Should_CreateOnlineSubscription_When_NoExistingOnline()
    {
        // Arrange
        var existing = Array.Empty<UserSubscription>();

        // Act
        var result = SubscriptionService.Create(
            UserId,
            SubscriptionKind.Online,
            city: null,
            categoryId: null,
            categoryExists: false,
            existing);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(SubscriptionKind.Online, result.Value.Kind);
        Assert.Null(result.Value.City);
        Assert.Null(result.Value.CategoryId);
    }

    [Fact]
    public void SubscriptionService_Should_ReturnDuplicate_When_CityDiffersOnlyByCasingAndWhitespace()
    {
        // Arrange
        var existing = new[]
        {
            UserSubscription.Create(UserId, SubscriptionKind.City, "sofia", categoryId: null)
        };

        // Act
        var result = SubscriptionService.Create(
            UserId,
            SubscriptionKind.City,
            "  SOFIA  ",
            categoryId: null,
            categoryExists: false,
            existing);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Subscriptions.Duplicate", result.Error.Code);
    }

    [Fact]
    public void SubscriptionService_Should_ReturnDuplicate_When_CategoryAlreadySubscribed()
    {
        // Arrange
        var existing = new[]
        {
            UserSubscription.Create(UserId, SubscriptionKind.Category, city: null, CategoryId)
        };

        // Act
        var result = SubscriptionService.Create(
            UserId,
            SubscriptionKind.Category,
            city: null,
            CategoryId,
            categoryExists: true,
            existing);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Subscriptions.Duplicate", result.Error.Code);
    }

    [Fact]
    public void SubscriptionService_Should_ReturnDuplicate_When_OnlineAlreadySubscribed()
    {
        // Arrange
        var existing = new[]
        {
            UserSubscription.Create(UserId, SubscriptionKind.Online, city: null, categoryId: null)
        };

        // Act
        var result = SubscriptionService.Create(
            UserId,
            SubscriptionKind.Online,
            city: null,
            categoryId: null,
            categoryExists: false,
            existing);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Subscriptions.Duplicate", result.Error.Code);
    }

    [Fact]
    public void SubscriptionService_Should_ReturnMaxLimitReached_When_UserHasThirtySubscriptions()
    {
        // Arrange
        var existing = Enumerable.Range(0, SubscriptionConstants.MaxSubscriptionsPerUser)
            .Select(i => UserSubscription.Create(
                UserId,
                SubscriptionKind.City,
                $"city-{i}",
                categoryId: null))
            .ToList();

        // Act
        var result = SubscriptionService.Create(
            UserId,
            SubscriptionKind.Online,
            city: null,
            categoryId: null,
            categoryExists: false,
            existing);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Subscriptions.MaxLimitReached", result.Error.Code);
    }

    [Fact]
    public void SubscriptionService_Should_ReturnCityRequired_When_CityKindHasBlankCity()
    {
        // Arrange
        var existing = Array.Empty<UserSubscription>();

        // Act
        var result = SubscriptionService.Create(
            UserId,
            SubscriptionKind.City,
            "   ",
            categoryId: null,
            categoryExists: false,
            existing);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Subscriptions.CityRequired", result.Error.Code);
    }

    [Fact]
    public void SubscriptionService_Should_ReturnCategoryRequired_When_CategoryKindHasNoId()
    {
        // Arrange
        var existing = Array.Empty<UserSubscription>();

        // Act
        var result = SubscriptionService.Create(
            UserId,
            SubscriptionKind.Category,
            city: null,
            categoryId: null,
            categoryExists: true,
            existing);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Subscriptions.CategoryRequired", result.Error.Code);
    }

    [Fact]
    public void SubscriptionService_Should_ReturnCategoryNotFound_When_CategoryDoesNotExist()
    {
        // Arrange
        var existing = Array.Empty<UserSubscription>();

        // Act
        var result = SubscriptionService.Create(
            UserId,
            SubscriptionKind.Category,
            city: null,
            CategoryId,
            categoryExists: false,
            existing);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Subscriptions.CategoryNotFound", result.Error.Code);
    }

    [Fact]
    public void SubscriptionService_Should_ReturnInvalidKindPayload_When_CityProvidedForOnline()
    {
        // Arrange
        var existing = Array.Empty<UserSubscription>();

        // Act
        var result = SubscriptionService.Create(
            UserId,
            SubscriptionKind.Online,
            "Sofia",
            categoryId: null,
            categoryExists: false,
            existing);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Subscriptions.InvalidKindPayload", result.Error.Code);
    }

    [Fact]
    public void SubscriptionService_Should_ReturnInvalidKindPayload_When_CategoryProvidedForCity()
    {
        // Arrange
        var existing = Array.Empty<UserSubscription>();

        // Act
        var result = SubscriptionService.Create(
            UserId,
            SubscriptionKind.City,
            "Sofia",
            CategoryId,
            categoryExists: true,
            existing);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Subscriptions.InvalidKindPayload", result.Error.Code);
    }

    [Fact]
    public void SubscriptionService_Should_ReturnSuccess_When_OwnerDeletesSubscription()
    {
        // Arrange
        var subscription = UserSubscription.Create(UserId, SubscriptionKind.Online, city: null, categoryId: null);

        // Act
        var result = SubscriptionService.Delete(subscription, UserId);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void SubscriptionService_Should_ReturnNotOwned_When_NonOwnerDeletesSubscription()
    {
        // Arrange
        var subscription = UserSubscription.Create(UserId, SubscriptionKind.Online, city: null, categoryId: null);
        var otherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        // Act
        var result = SubscriptionService.Delete(subscription, otherUserId);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Subscriptions.NotOwned", result.Error.Code);
    }
}
