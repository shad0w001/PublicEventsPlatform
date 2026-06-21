using Application.Abstractions.Authentication;
using Application.Events.GetMyFeed;
using Application.Events.Services;
using Application.Users;
using Application.Users.Services;
using Domain.Events;
using Domain.Events.EventLocations;
using Domain.Events.Services;
using Domain.Subscriptions;
using Domain.Users;
using Domain.Users.Services;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApplicationTests.Events.GetMyFeed;

public class GetMyFeedQueryHandlerTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";

    private static readonly DateTime FutureStart = new(2026, 8, 1, 18, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FutureEnd = new(2026, 8, 1, 22, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PastStart = new(2020, 1, 1, 18, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PastEnd = new(2020, 1, 1, 22, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetMyFeedQueryHandler_Should_ReturnEmptyPagedResult_When_UserHasNoSubscriptions()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|feed-empty", "feed-empty@example.com");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);
        await ProvisionUserAsync(context, identity);

        // Act
        var result = await handler.Handle(new GetMyFeedQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Items);
        Assert.Equal(0, result.Value.TotalCount);
    }

    [Fact]
    public async Task GetMyFeedQueryHandler_Should_ReturnCityMatchOnly_When_CitySubscriptionOnly()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|feed-city", "feed-city@example.com");
        var user = SeedUser(databaseName);
        await SeedBrowsableEventAsync(databaseName, user, "Sofia Event", FutureStart, FutureEnd, city: "Sofia");
        await SeedBrowsableEventAsync(databaseName, user, "Plovdiv Event", FutureStart, FutureEnd, city: "Plovdiv");

        await using var seedContext = CreateContext(databaseName);
        var subscriber = await ProvisionUserAsync(seedContext, identity);
        seedContext.UserSubscriptions.Add(
            UserSubscription.Create(subscriber.Id, SubscriptionKind.City, "sofia", null));
        await seedContext.SaveChangesAsync(CancellationToken.None);

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new GetMyFeedQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal("Sofia Event", result.Value.Items[0].Title);
    }

    [Fact]
    public async Task GetMyFeedQueryHandler_Should_ReturnVirtualSegmentEvents_When_OnlineSubscriptionOnly()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|feed-online", "feed-online@example.com");
        var user = SeedUser(databaseName);
        await SeedBrowsableEventAsync(
            databaseName,
            user,
            "Hybrid Stream",
            FutureStart,
            FutureEnd,
            locations:
            [
                new EventLocation
                {
                    Name = "Venue",
                    Kind = EventLocationKind.Physical,
                    Address = "1 Main St",
                    City = "Sofia"
                },
                new EventLocation
                {
                    Name = "Stream",
                    Kind = EventLocationKind.Virtual,
                    Url = "https://stream.example/live"
                }
            ],
            locationType: EventLocationType.Hybrid);
        await SeedBrowsableEventAsync(databaseName, user, "Physical Only", FutureStart, FutureEnd, city: "Sofia");

        await using var seedContext = CreateContext(databaseName);
        var subscriber = await ProvisionUserAsync(seedContext, identity);
        seedContext.UserSubscriptions.Add(
            UserSubscription.Create(subscriber.Id, SubscriptionKind.Online, null, null));
        await seedContext.SaveChangesAsync(CancellationToken.None);

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new GetMyFeedQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal("Hybrid Stream", result.Value.Items[0].Title);
        Assert.Equal(EventLocationType.Hybrid, result.Value.Items[0].LocationType);
    }

    [Fact]
    public async Task GetMyFeedQueryHandler_Should_ReturnGlobalCategoryMatch_When_CategorySubscriptionOnly()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|feed-cat", "feed-cat@example.com");
        var user = SeedUser(databaseName);
        var (parentId, childId) = SeedCategoryTree(databaseName);
        await SeedBrowsableEventAsync(
            databaseName,
            user,
            "Child Category Event",
            FutureStart,
            FutureEnd,
            categoryId: childId,
            city: "Plovdiv");
        await SeedBrowsableEventAsync(
            databaseName,
            user,
            "Other Category Event",
            FutureStart,
            FutureEnd,
            categoryId: parentId,
            city: "Sofia");
        var sportsId = SeedCategory(databaseName, "Sports");
        await SeedBrowsableEventAsync(
            databaseName,
            user,
            "Sports Event",
            FutureStart,
            FutureEnd,
            categoryId: sportsId,
            city: "Sofia");

        await using var seedContext = CreateContext(databaseName);
        var subscriber = await ProvisionUserAsync(seedContext, identity);
        seedContext.UserSubscriptions.Add(
            UserSubscription.Create(subscriber.Id, SubscriptionKind.Category, null, parentId));
        await seedContext.SaveChangesAsync(CancellationToken.None);

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new GetMyFeedQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalCount);
        Assert.Contains(result.Value.Items, i => i.Title == "Child Category Event");
        Assert.Contains(result.Value.Items, i => i.Title == "Other Category Event");
        Assert.DoesNotContain(result.Value.Items, i => i.Title == "Sports Event");
    }

    [Fact]
    public async Task GetMyFeedQueryHandler_Should_RequireCityAndCategory_When_BothSubscriptionKindsExist()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|feed-and", "feed-and@example.com");
        var user = SeedUser(databaseName);
        var musicId = SeedCategory(databaseName, "Music");
        var sportsId = SeedCategory(databaseName, "Sports");
        await SeedBrowsableEventAsync(
            databaseName,
            user,
            "Sofia Music",
            FutureStart,
            FutureEnd,
            categoryId: musicId,
            city: "Sofia");
        await SeedBrowsableEventAsync(
            databaseName,
            user,
            "Plovdiv Music",
            FutureStart,
            FutureEnd,
            categoryId: musicId,
            city: "Plovdiv");
        await SeedBrowsableEventAsync(
            databaseName,
            user,
            "Sofia Sports",
            FutureStart,
            FutureEnd,
            categoryId: sportsId,
            city: "Sofia");

        await using var seedContext = CreateContext(databaseName);
        var subscriber = await ProvisionUserAsync(seedContext, identity);
        seedContext.UserSubscriptions.Add(
            UserSubscription.Create(subscriber.Id, SubscriptionKind.City, "sofia", null));
        seedContext.UserSubscriptions.Add(
            UserSubscription.Create(subscriber.Id, SubscriptionKind.Category, null, musicId));
        await seedContext.SaveChangesAsync(CancellationToken.None);

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new GetMyFeedQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal("Sofia Music", result.Value.Items[0].Title);
    }

    [Fact]
    public async Task GetMyFeedQueryHandler_Should_MatchEitherCity_When_MultipleCitySubscriptionsExist()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|feed-cities-or", "feed-cities-or@example.com");
        var user = SeedUser(databaseName);
        await SeedBrowsableEventAsync(databaseName, user, "Sofia Event", FutureStart, FutureEnd, city: "Sofia");
        await SeedBrowsableEventAsync(databaseName, user, "Plovdiv Event", FutureStart, FutureEnd, city: "Plovdiv");
        await SeedBrowsableEventAsync(databaseName, user, "Varna Event", FutureStart, FutureEnd, city: "Varna");

        await using var seedContext = CreateContext(databaseName);
        var subscriber = await ProvisionUserAsync(seedContext, identity);
        seedContext.UserSubscriptions.Add(
            UserSubscription.Create(subscriber.Id, SubscriptionKind.City, "sofia", null));
        seedContext.UserSubscriptions.Add(
            UserSubscription.Create(subscriber.Id, SubscriptionKind.City, "plovdiv", null));
        await seedContext.SaveChangesAsync(CancellationToken.None);

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new GetMyFeedQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalCount);
        Assert.Contains(result.Value.Items, i => i.Title == "Sofia Event");
        Assert.Contains(result.Value.Items, i => i.Title == "Plovdiv Event");
        Assert.DoesNotContain(result.Value.Items, i => i.Title == "Varna Event");
    }

    [Fact]
    public async Task GetMyFeedQueryHandler_Should_ExcludeNonPublishedFutureEvents_When_DefaultFeed()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|feed-exclude", "feed-exclude@example.com");
        var user = SeedUser(databaseName);
        await SeedBrowsableEventAsync(databaseName, user, "Published Future", FutureStart, FutureEnd);
        await SeedBrowsableEventAsync(
            databaseName,
            user,
            "Draft",
            FutureStart,
            FutureEnd,
            publish: false);
        await SeedBrowsableEventAsync(
            databaseName,
            user,
            "Cancelled",
            FutureStart,
            FutureEnd,
            cancel: true);
        await SeedBrowsableEventAsync(databaseName, user, "Past", PastStart, PastEnd);
        await SeedBrowsableEventAsync(
            databaseName,
            user,
            "Deleted",
            FutureStart,
            FutureEnd,
            softDelete: true);

        await using var seedContext = CreateContext(databaseName);
        var subscriber = await ProvisionUserAsync(seedContext, identity);
        seedContext.UserSubscriptions.Add(
            UserSubscription.Create(subscriber.Id, SubscriptionKind.City, "sofia", null));
        await seedContext.SaveChangesAsync(CancellationToken.None);

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new GetMyFeedQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal("Published Future", result.Value.Items[0].Title);
    }

    [Fact]
    public async Task GetMyFeedQueryHandler_Should_PaginateResults_When_MoreEventsThanPageSize()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|feed-page", "feed-page@example.com");
        var user = SeedUser(databaseName);
        for (var i = 0; i < 5; i++)
        {
            var start = FutureStart.AddDays(i);
            await SeedBrowsableEventAsync(
                databaseName,
                user,
                $"Event {i}",
                start,
                start.AddHours(4));
        }

        await using var seedContext = CreateContext(databaseName);
        var subscriber = await ProvisionUserAsync(seedContext, identity);
        seedContext.UserSubscriptions.Add(
            UserSubscription.Create(subscriber.Id, SubscriptionKind.City, "sofia", null));
        await seedContext.SaveChangesAsync(CancellationToken.None);

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var page1 = await handler.Handle(new GetMyFeedQuery(Page: 1, PageSize: 2), CancellationToken.None);
        var page2 = await handler.Handle(new GetMyFeedQuery(Page: 2, PageSize: 2), CancellationToken.None);

        // Assert
        Assert.True(page1.IsSuccess);
        Assert.True(page2.IsSuccess);
        Assert.Equal(5, page1.Value.TotalCount);
        Assert.Equal(2, page1.Value.Items.Count);
        Assert.Equal(2, page2.Value.Items.Count);
        Assert.Equal("Event 0", page1.Value.Items[0].Title);
        Assert.Equal("Event 2", page2.Value.Items[0].Title);
    }

    [Fact]
    public async Task GetMyFeedQueryHandler_Should_ReturnEmailNotVerified_When_CallerIsUnverified()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = new FakeUserIdentityAccessor
        {
            IsAuthenticated = true,
            ExternalSubjectId = "auth0|feed-unverified",
            Email = "feed-unverified@example.com",
            EmailVerified = false
        };
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new GetMyFeedQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Users.EmailNotVerified", result.Error.Code);
    }

    [Fact]
    public async Task GetMyFeedQueryHandler_Should_MapCardFields_When_EventMatches()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|feed-card", "feed-card@example.com");
        var user = SeedUser(databaseName);
        var categoryId = SeedCategory(databaseName, "Music");
        const string bannerUrl = "/uploads/events/feed-card.webp";
        await SeedBrowsableEventAsync(
            databaseName,
            user,
            "Feed Card Event",
            FutureStart,
            FutureEnd,
            categoryId: categoryId,
            bannerUrl: bannerUrl);

        await using var seedContext = CreateContext(databaseName);
        var subscriber = await ProvisionUserAsync(seedContext, identity);
        seedContext.UserSubscriptions.Add(
            UserSubscription.Create(subscriber.Id, SubscriptionKind.Category, null, categoryId));
        await seedContext.SaveChangesAsync(CancellationToken.None);

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new GetMyFeedQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var card = result.Value.Items.Single();
        Assert.Equal("Feed Card Event", card.Title);
        Assert.Equal(bannerUrl, card.BannerImageUrl);
        Assert.Equal("Europe/Sofia", card.TimeZoneId);
        Assert.Equal(EventLocationType.Physical, card.LocationType);
        Assert.Equal(AdmissionType.Free, card.AdmissionType);
        Assert.Equal(categoryId, card.CategoryId);
        Assert.Equal("Music", card.CategoryName);
        Assert.False(card.HostIsGroup);
    }

    private static User SeedUser(string databaseName)
    {
        var user = UserService.ProvisionFromExternalIdentity(
            $"auth0|{Guid.NewGuid():N}",
            $"user-{Guid.NewGuid():N}@example.com",
            emailVerified: true,
            profilePictureUrl: null,
            DefaultAvatarUrl,
            ServiceRole.User);
        user.Username = "feed-host";

        using var context = CreateContext(databaseName);
        context.Users.Add(user);
        context.SaveChanges();
        return user;
    }

    private static Guid SeedCategory(string databaseName, string name)
    {
        using var context = CreateContext(databaseName);
        var category = new EventCategory { Name = name };
        context.EventCategories.Add(category);
        context.SaveChanges();
        return category.Id;
    }

    private static (Guid ParentId, Guid ChildId) SeedCategoryTree(string databaseName)
    {
        using var context = CreateContext(databaseName);
        var parent = new EventCategory { Name = "Music" };
        var child = new EventCategory { Name = "Live Music", ParentCategory = parent };
        context.EventCategories.AddRange(parent, child);
        context.SaveChanges();
        return (parent.Id, child.Id);
    }

    private static async Task<Guid> SeedBrowsableEventAsync(
        string databaseName,
        User user,
        string title,
        DateTime start,
        DateTime end,
        Guid? categoryId = null,
        string city = "Sofia",
        EventTier tier = EventTier.Small,
        AdmissionType admissionType = AdmissionType.Free,
        EventLocationType locationType = EventLocationType.Physical,
        IReadOnlyList<EventLocation>? locations = null,
        bool publish = true,
        bool cancel = false,
        bool softDelete = false,
        string? bannerUrl = null)
    {
        await using var context = CreateContext(databaseName);
        var createResult = EventService.Create(tier, title, user.Id);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;

        var resolvedCategoryId = categoryId ?? SeedCategory(databaseName, "General");
        var segmentLocations = locations ??
        [
            new EventLocation
            {
                Name = "Main Hall",
                Kind = EventLocationKind.Physical,
                Address = "123 Main St",
                City = city
            }
        ];

        var patch = new EventUpdatePatch
        {
            Description = "Feed test event description",
            CategoryId = resolvedCategoryId,
            StartTime = start,
            EndTime = end,
            TimeZoneId = "Europe/Sofia",
            AdmissionType = admissionType,
            Locations = segmentLocations.ToList()
        };

        var updateResult = EventService.Update(@event, patch, user.Id);
        if (updateResult.IsFailure)
        {
            throw new InvalidOperationException(updateResult.Error.Code);
        }

        if (bannerUrl is not null)
        {
            EventService.Update(@event, new EventUpdatePatch { BannerImageUrl = bannerUrl }, user.Id);
        }

        if (locationType != @event.LocationType)
        {
            @event.LocationType = locationType;
        }

        if (softDelete)
        {
            EventService.SoftDelete(@event);
        }
        else if (publish)
        {
            EventService.Publish(@event, categoryExists: true, recentPublishCount: 0, maxPublishesPerWeek: 100);
            if (cancel)
            {
                EventService.Cancel(@event);
            }
        }

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
        return @event.Id;
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

    private static GetMyFeedQueryHandler CreateHandler(
        ApplicationDbContext context,
        FakeUserIdentityAccessor identity)
    {
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));

        return new GetMyFeedQueryHandler(
            context,
            currentUserService,
            identity,
            new EventAccessService(context));
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
