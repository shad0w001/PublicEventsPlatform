using Application.Events.BrowseEvents;
using Application.Events.Services;
using Domain.Events;
using Domain.Events.EventLocations;
using Domain.Events.Services;
using Domain.Users;
using Domain.Users.Services;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace ApplicationTests.Events.BrowseEvents;

public class BrowseEventsQueryHandlerTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";

    private static readonly DateTime FutureStart = new(2026, 8, 1, 18, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FutureEnd = new(2026, 8, 1, 22, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PastStart = new(2020, 1, 1, 18, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PastEnd = new(2020, 1, 1, 22, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task BrowseEventsQueryHandler_Should_ReturnOnlyPublishedFutureEvents_When_DefaultBrowse()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var user = SeedUser(databaseName);
        var publishedId = await SeedBrowsableEventAsync(
            databaseName,
            user,
            "Published Future",
            FutureStart,
            FutureEnd);
        await SeedBrowsableEventAsync(
            databaseName,
            user,
            "Draft Event",
            FutureStart,
            FutureEnd,
            publish: false);
        await SeedBrowsableEventAsync(
            databaseName,
            user,
            "Cancelled Event",
            FutureStart,
            FutureEnd,
            cancel: true);
        await SeedBrowsableEventAsync(
            databaseName,
            user,
            "Past Event",
            PastStart,
            PastEnd);
        await SeedBrowsableEventAsync(
            databaseName,
            user,
            "Deleted Event",
            FutureStart,
            FutureEnd,
            softDelete: true);

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context);

        // Act
        var result = await handler.Handle(new BrowseEventsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalCount);
        Assert.Single(result.Value.Items);
        Assert.Equal(publishedId, result.Value.Items[0].Id);
        Assert.Equal("Published Future", result.Value.Items[0].Title);
    }

    [Fact]
    public async Task BrowseEventsQueryHandler_Should_IncludeChildCategory_When_ParentCategoryFilterApplied()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var user = SeedUser(databaseName);
        var (parentId, childId) = SeedCategoryTree(databaseName);
        await SeedBrowsableEventAsync(
            databaseName,
            user,
            "Live Music Night",
            FutureStart,
            FutureEnd,
            categoryId: childId);
        await SeedBrowsableEventAsync(
            databaseName,
            user,
            "Sports Day",
            FutureStart,
            FutureEnd,
            categoryId: parentId,
            city: "Plovdiv");

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context);

        // Act
        var result = await handler.Handle(
            new BrowseEventsQuery(CategoryId: [parentId]),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalCount);
        Assert.Contains(result.Value.Items, i => i.Title == "Live Music Night");
        Assert.Contains(result.Value.Items, i => i.Title == "Sports Day");
    }

    [Fact]
    public async Task BrowseEventsQueryHandler_Should_ApplyOrAcrossCategories_When_MultipleCategoryIdsProvided()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var user = SeedUser(databaseName);
        var musicId = SeedCategory(databaseName, "Music");
        var sportsId = SeedCategory(databaseName, "Sports");
        var techId = SeedCategory(databaseName, "Technology");

        await SeedBrowsableEventAsync(databaseName, user, "Music Event", FutureStart, FutureEnd, categoryId: musicId);
        await SeedBrowsableEventAsync(databaseName, user, "Sports Event", FutureStart, FutureEnd, categoryId: sportsId);
        await SeedBrowsableEventAsync(databaseName, user, "Tech Event", FutureStart, FutureEnd, categoryId: techId);

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context);

        // Act
        var result = await handler.Handle(
            new BrowseEventsQuery(CategoryId: [musicId, sportsId]),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalCount);
        Assert.DoesNotContain(result.Value.Items, i => i.Title == "Tech Event");
    }

    [Fact]
    public async Task BrowseEventsQueryHandler_Should_ApplyAndAcrossDimensions_When_CityAndTierFiltersCombined()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var user = SeedUser(databaseName);
        await SeedBrowsableEventAsync(
            databaseName,
            user,
            "Sofia Small",
            FutureStart,
            FutureEnd,
            city: "Sofia",
            tier: EventTier.Small);
        await SeedBrowsableEventAsync(
            databaseName,
            user,
            "Sofia Big",
            FutureStart,
            FutureEnd,
            city: "Sofia",
            tier: EventTier.Big);
        await SeedBrowsableEventAsync(
            databaseName,
            user,
            "Plovdiv Small",
            FutureStart,
            FutureEnd,
            city: "Plovdiv",
            tier: EventTier.Small);

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context);

        // Act
        var result = await handler.Handle(
            new BrowseEventsQuery(City: ["Sofia"], Tier: [EventTier.Small]),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal("Sofia Small", result.Value.Items[0].Title);
    }

    [Fact]
    public async Task BrowseEventsQueryHandler_Should_ApplyOrWithinCities_When_MultipleCitiesProvided()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var user = SeedUser(databaseName);
        await SeedBrowsableEventAsync(databaseName, user, "Sofia Event", FutureStart, FutureEnd, city: "Sofia");
        await SeedBrowsableEventAsync(databaseName, user, "Plovdiv Event", FutureStart, FutureEnd, city: "Plovdiv");
        await SeedBrowsableEventAsync(databaseName, user, "Varna Event", FutureStart, FutureEnd, city: "Varna");

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context);

        // Act
        var result = await handler.Handle(
            new BrowseEventsQuery(City: ["Sofia", "plovdiv"]),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalCount);
    }

    [Fact]
    public async Task BrowseEventsQueryHandler_Should_FilterByLocationTypeAndAdmissionType_When_Provided()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var user = SeedUser(databaseName);
        await SeedBrowsableEventAsync(
            databaseName,
            user,
            "Online Free",
            FutureStart,
            FutureEnd,
            locationType: EventLocationType.Online,
            locations:
            [
                new EventLocation
                {
                    Name = "Stream",
                    Kind = EventLocationKind.Virtual,
                    Url = "https://example.com/live"
                }
            ]);
        await SeedBrowsableEventAsync(
            databaseName,
            user,
            "Physical Free",
            FutureStart,
            FutureEnd,
            admissionType: AdmissionType.Free,
            locationType: EventLocationType.Physical);

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context);

        // Act
        var onlineResult = await handler.Handle(
            new BrowseEventsQuery(LocationType: [EventLocationType.Online]),
            CancellationToken.None);
        var freeResult = await handler.Handle(
            new BrowseEventsQuery(AdmissionType: [AdmissionType.Free]),
            CancellationToken.None);

        // Assert
        Assert.True(onlineResult.IsSuccess);
        Assert.Single(onlineResult.Value.Items);
        Assert.Equal("Online Free", onlineResult.Value.Items[0].Title);

        Assert.True(freeResult.IsSuccess);
        Assert.Equal(2, freeResult.Value.TotalCount);
    }

    [Fact]
    public async Task BrowseEventsQueryHandler_Should_FilterByStartTimeRange_When_StartFromAndStartToProvided()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var user = SeedUser(databaseName);
        var earlyStart = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        var lateStart = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);

        await SeedBrowsableEventAsync(databaseName, user, "Early", earlyStart, earlyStart.AddHours(2));
        await SeedBrowsableEventAsync(databaseName, user, "Late", lateStart, lateStart.AddHours(2));

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context);

        // Act
        var result = await handler.Handle(
            new BrowseEventsQuery(
                StartFrom: new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc),
                StartTo: new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc)),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal("Late", result.Value.Items[0].Title);
    }

    [Fact]
    public async Task BrowseEventsQueryHandler_Should_PaginateResults_When_PageAndPageSizeProvided()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var user = SeedUser(databaseName);
        for (var i = 0; i < 5; i++)
        {
            var start = FutureStart.AddDays(i);
            await SeedBrowsableEventAsync(databaseName, user, $"Event {i}", start, start.AddHours(2));
        }

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context);

        // Act
        var result = await handler.Handle(
            new BrowseEventsQuery(Page: 2, PageSize: 2),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value.TotalCount);
        Assert.Equal(2, result.Value.Page);
        Assert.Equal(2, result.Value.PageSize);
        Assert.Equal(2, result.Value.Items.Count);
        Assert.Equal("Event 2", result.Value.Items[0].Title);
        Assert.Equal("Event 3", result.Value.Items[1].Title);
    }

    [Fact]
    public async Task BrowseEventsQueryHandler_Should_ReturnInvalidDateRange_When_StartFromAfterStartTo()
    {
        // Arrange
        await using var context = CreateContext(Guid.NewGuid().ToString());
        var handler = CreateHandler(context);

        // Act
        var result = await handler.Handle(
            new BrowseEventsQuery(
                StartFrom: new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc),
                StartTo: new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc)),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.Discovery.InvalidDateRange", result.Error.Code);
    }

    [Fact]
    public async Task BrowseEventsQueryHandler_Should_MatchTextQuery_When_TitleContainsTerm()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var user = SeedUser(databaseName);
        await SeedBrowsableEventAsync(databaseName, user, "Jazz Music Festival", FutureStart, FutureEnd);
        await SeedBrowsableEventAsync(databaseName, user, "Football Match", FutureStart, FutureEnd);

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context);

        // Act
        var result = await handler.Handle(new BrowseEventsQuery(Q: "jazz"), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal("Jazz Music Festival", result.Value.Items[0].Title);
    }

    [Fact]
    public async Task BrowseEventsQueryHandler_Should_MatchNormalizedCity_When_EventCityHasExtraWhitespace()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var user = SeedUser(databaseName);
        await SeedBrowsableEventAsync(
            databaseName,
            user,
            "New York Concert",
            FutureStart,
            FutureEnd,
            city: "New  York");
        await SeedBrowsableEventAsync(
            databaseName,
            user,
            "Los Angeles Show",
            FutureStart,
            FutureEnd,
            city: "Los Angeles");

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context);

        // Act
        var result = await handler.Handle(
            new BrowseEventsQuery(City: ["New York"]),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal("New York Concert", result.Value.Items[0].Title);
    }

    [Fact]
    public async Task BrowseEventsQueryHandler_Should_FilterByCountry_When_CountryProvided()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var user = SeedUser(databaseName);
        await SeedBrowsableEventAsync(
            databaseName,
            user,
            "Bulgaria Event",
            FutureStart,
            FutureEnd,
            city: "Sofia",
            country: "Bulgaria");
        await SeedBrowsableEventAsync(
            databaseName,
            user,
            "Germany Event",
            FutureStart,
            FutureEnd,
            city: "Berlin",
            country: "Germany");

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context);

        // Act
        var result = await handler.Handle(
            new BrowseEventsQuery(Country: ["Bulgaria"]),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal("Bulgaria Event", result.Value.Items[0].Title);
    }

    [Fact]
    public async Task BrowseEventsQueryHandler_Should_FilterByHybridLocationType_When_HybridEventExists()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var user = SeedUser(databaseName);
        await SeedBrowsableEventAsync(
            databaseName,
            user,
            "Hybrid Conference",
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
            ]);
        await SeedBrowsableEventAsync(databaseName, user, "Physical Only", FutureStart, FutureEnd, city: "Sofia");

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context);

        // Act
        var result = await handler.Handle(
            new BrowseEventsQuery(LocationType: [EventLocationType.Hybrid]),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal("Hybrid Conference", result.Value.Items[0].Title);
        Assert.Equal(EventLocationType.Hybrid, result.Value.Items[0].LocationType);
    }

    [Fact]
    public async Task BrowseEventsQueryHandler_Should_MapCardFields_When_EventMatches()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var user = SeedUser(databaseName);
        var categoryId = SeedCategory(databaseName, "Music");
        const string bannerUrl = "/uploads/events/card.webp";
        await SeedBrowsableEventAsync(
            databaseName,
            user,
            "Card Event",
            FutureStart,
            FutureEnd,
            categoryId: categoryId,
            bannerUrl: bannerUrl);

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context);

        // Act
        var result = await handler.Handle(new BrowseEventsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var card = result.Value.Items.Single();
        Assert.Equal("Card Event", card.Title);
        Assert.Equal(bannerUrl, card.BannerImageUrl);
        Assert.Equal("Europe/Sofia", card.TimeZoneId);
        Assert.Equal(EventLocationType.Physical, card.LocationType);
        Assert.Equal(AdmissionType.Free, card.AdmissionType);
        Assert.Equal(EventTier.Small, card.Tier);
        Assert.Equal(categoryId, card.CategoryId);
        Assert.Equal("Music", card.CategoryName);
        Assert.False(card.HostIsGroup);
        Assert.False(string.IsNullOrWhiteSpace(card.HostDisplayName));
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
        user.Username = "browse-host";

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
        string? country = null,
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
                City = city,
                Country = country
            }
        ];

        var patch = new EventUpdatePatch
        {
            Description = "Browse test event description",
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

    private static BrowseEventsQueryHandler CreateHandler(ApplicationDbContext context) =>
        new(context, new EventAccessService(context));

    private static ApplicationDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new ApplicationDbContext(options);
    }
}
