using Domain.Subscriptions;
using Domain.Subscriptions.Services;
using Domain.Users;
using Domain.Users.Services;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureTests;

public class SubscriptionModelTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";

    [Fact]
    public void UserSubscriptionModel_Should_MapToUserSubscriptionsTable_When_ApplicationDbContextModelIsBuilt()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        var entityType = context.Model.FindEntityType(typeof(UserSubscription));

        // Assert
        Assert.NotNull(entityType);
        Assert.Equal("user_subscriptions", entityType.GetTableName());
        Assert.NotNull(entityType.FindProperty(nameof(UserSubscription.Kind)));
        Assert.NotNull(entityType.FindProperty(nameof(UserSubscription.City)));
        Assert.NotNull(entityType.FindProperty(nameof(UserSubscription.CategoryId)));
    }

    [Fact]
    public void UserSubscriptionModel_Should_NotContainShadowUserId1_When_ApplicationDbContextModelIsBuilt()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        var entityType = context.Model.FindEntityType(typeof(UserSubscription));
        var shadowProperty = entityType?.FindProperty("UserId1");

        // Assert
        Assert.NotNull(entityType);
        Assert.Null(shadowProperty);
    }

    [Fact]
    public void UserSubscriptionModel_Should_HaveFilteredUniqueIndexes_When_ApplicationDbContextModelIsBuilt()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        var entityType = context.Model.FindEntityType(typeof(UserSubscription));
        var indexes = entityType?.GetIndexes().ToList() ?? [];

        var cityIndex = indexes.SingleOrDefault(i =>
            i.Properties.Count == 2 &&
            i.Properties.Any(p => p.Name == nameof(UserSubscription.City)) &&
            i.IsUnique &&
            i.GetFilter()?.Contains("City") == true);

        var categoryIndex = indexes.SingleOrDefault(i =>
            i.Properties.Count == 2 &&
            i.Properties.Any(p => p.Name == nameof(UserSubscription.CategoryId)) &&
            i.IsUnique &&
            i.GetFilter()?.Contains("Category") == true);

        var onlineIndex = indexes.SingleOrDefault(i =>
            i.Properties.Count == 1 &&
            i.Properties.Any(p => p.Name == nameof(UserSubscription.UserId)) &&
            i.IsUnique &&
            i.GetFilter()?.Contains("Online") == true);

        // Assert
        Assert.NotNull(cityIndex);
        Assert.NotNull(categoryIndex);
        Assert.NotNull(onlineIndex);
    }

    [Fact]
    public async Task UserSubscription_Should_PersistAllKinds_When_UserSubscriptionsAreSaved()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var user = UserService.ProvisionFromExternalIdentity(
            "auth0|subscription-user",
            "subscriber@example.com",
            emailVerified: true,
            profilePictureUrl: null,
            DefaultAvatarUrl,
            ServiceRole.User);

        var categoryId = Guid.Parse("e0000001-0001-4000-8000-000000000001");

        var cityResult = SubscriptionService.Create(
            user.Id,
            SubscriptionKind.City,
            "  Sofia  ",
            categoryId: null,
            categoryExists: false,
            []);
        var categoryResult = SubscriptionService.Create(
            user.Id,
            SubscriptionKind.Category,
            city: null,
            categoryId,
            categoryExists: true,
            []);
        var onlineResult = SubscriptionService.Create(
            user.Id,
            SubscriptionKind.Online,
            city: null,
            categoryId: null,
            categoryExists: false,
            []);

        // Act
        await using (var context = CreateContext(databaseName))
        {
            context.Users.Add(user);
            context.UserSubscriptions.AddRange(
                cityResult.Value,
                categoryResult.Value,
                onlineResult.Value);
            await context.SaveChangesAsync();
        }

        List<UserSubscription> loaded;
        await using (var context = CreateContext(databaseName))
        {
            loaded = await context.UserSubscriptions
                .Where(s => s.UserId == user.Id)
                .OrderBy(s => s.Kind)
                .ToListAsync();
        }

        // Assert
        Assert.Equal(3, loaded.Count);
        Assert.Equal("sofia", loaded.Single(s => s.Kind == SubscriptionKind.City).City);
        Assert.Equal(categoryId, loaded.Single(s => s.Kind == SubscriptionKind.Category).CategoryId);
        Assert.Equal(SubscriptionKind.Online, loaded.Single(s => s.Kind == SubscriptionKind.Online).Kind);
    }

    private static ApplicationDbContext CreateContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
