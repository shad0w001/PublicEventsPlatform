using Application.Events.Services;
using Domain.Events;
using Domain.Events.EventLocations;
using Domain.Events.Services;
using Domain.Users;
using Domain.Users.Services;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace ApplicationTests.Events.Services;

public class EventTextSearchHelperTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";
    [Fact]
    public void EventTextSearchHelper_Should_MatchTitle_When_TermAppearsInTitle()
    {
        // Arrange
        var @event = CreateEvent("Jazz Music Festival", "Outdoor concert", "Sofia");

        // Act
        var matches = EventTextSearchHelper.MatchesText(@event, "jazz");

        // Assert
        Assert.True(matches);
    }

    [Fact]
    public void EventTextSearchHelper_Should_MatchCategoryName_When_TermAppearsInCategory()
    {
        // Arrange
        var @event = CreateEvent("Community Meetup", "Monthly gathering", "Sofia");
        @event.Category = new EventCategory { Name = "Music" };

        // Act
        var matches = EventTextSearchHelper.MatchesText(@event, "music");

        // Assert
        Assert.True(matches);
    }

    [Fact]
    public void EventTextSearchHelper_Should_NotMatch_When_TermMissingFromSearchableFields()
    {
        // Arrange
        var @event = CreateEvent("Football Match", "Local derby", "Plovdiv");

        // Act
        var matches = EventTextSearchHelper.MatchesText(@event, "jazz");

        // Assert
        Assert.False(matches);
    }

    [Fact]
    public async Task EventDiscoveryQueryService_Should_SkipContains_When_ApplyTextContainsFilterIsFalse()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        await using var context = CreateContext(databaseName);
        var user = SeedUser(context);
        await SeedPublishedEventAsync(context, user, "Jazz Music Festival");
        await SeedPublishedEventAsync(context, user, "Football Match");

        var criteria = new EventDiscoveryCriteria(
            DateTime.UtcNow,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            "jazz");

        // Act
        var filtered = EventDiscoveryQueryService.Apply(
            context.Events.AsNoTracking(),
            criteria,
            applyTextContainsFilter: false);

        var titles = await filtered.Select(e => e.Title).ToListAsync();

        // Assert
        Assert.Equal(2, titles.Count);
        Assert.Contains("Jazz Music Festival", titles);
        Assert.Contains("Football Match", titles);
    }

    private static ApplicationDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new ApplicationDbContext(options);
    }

    private static User SeedUser(ApplicationDbContext context)
    {
        var user = UserService.ProvisionFromExternalIdentity(
            $"auth0|{Guid.NewGuid():N}",
            $"search-{Guid.NewGuid():N}@example.com",
            emailVerified: true,
            profilePictureUrl: null,
            DefaultAvatarUrl,
            ServiceRole.User);

        context.Users.Add(user);
        context.SaveChanges();
        return user;
    }

    private static Event CreateEvent(string title, string description, string city)
    {
        var createResult = EventService.Create(EventTier.Small, title, Guid.NewGuid());
        var @event = createResult.Value.Event;

        EventService.Update(@event, new EventUpdatePatch { Description = description }, Guid.NewGuid());
        @event.Locations =
        [
            new EventLocation
            {
                Name = "Venue",
                Kind = EventLocationKind.Physical,
                City = city
            }
        ];

        return @event;
    }

    private static async Task SeedPublishedEventAsync(
        ApplicationDbContext context,
        User user,
        string title)
    {
        var start = new DateTime(2026, 8, 1, 18, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 8, 1, 22, 0, 0, DateTimeKind.Utc);
        var category = new EventCategory { Name = "General" };
        context.EventCategories.Add(category);

        var createResult = EventService.Create(EventTier.Small, title, user.Id);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;

        var updateResult = EventService.Update(@event, new EventUpdatePatch
        {
            Description = "Description",
            CategoryId = category.Id,
            StartTime = start,
            EndTime = end,
            TimeZoneId = "UTC",
            AdmissionType = AdmissionType.Free,
            Locations =
            [
                new EventLocation
                {
                    Name = "Venue",
                    Kind = EventLocationKind.Physical,
                    City = "sofia"
                }
            ]
        }, user.Id);

        if (updateResult.IsFailure)
        {
            throw new InvalidOperationException(updateResult.Error.Code);
        }

        EventService.Publish(@event, categoryExists: true, recentPublishCount: 0, maxPublishesPerWeek: 100);

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync();
    }
}
