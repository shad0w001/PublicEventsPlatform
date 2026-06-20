using Application.Abstractions.Authentication;
using Application.Events;
using Application.Events.CreateEvent;
using Application.Events.UpdateEvent;
using Application.Events.Services;
using Application.Users;
using Application.Users.Services;
using Domain.Events;
using Domain.Events.EventLocations;
using Domain.Events.Services;
using Domain.Groups;
using Domain.Groups.Services;
using Domain.Plugins;
using Domain.Plugins.Services;
using Domain.Tickets.Services;
using Domain.Users;
using Domain.Users.Services;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApplicationTests.Events.UpdateEvent;

public class UpdateEventCommandHandlerTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";
    private const string DefaultGroupImageUrl = "/images/default-group.png";

    [Fact]
    public async Task UpdateEventCommandHandler_Should_SetCreatedByUserId_When_FirstPatchApplied()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|update-first-patch", "first@example.com");
        var eventId = await SeedDraftEventAsync(databaseName, identity, "Original Title");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var command = new UpdateEventCommand(eventId, Description: "Event description");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.CreatedByUserId);

        await using var verifyContext = CreateContext(databaseName);
        var user = await verifyContext.Users.SingleAsync();
        var persistedEvent = await verifyContext.Events.SingleAsync();
        Assert.Equal(user.Id, persistedEvent.CreatedByUserId);
        Assert.Equal(user.Id, result.Value.CreatedByUserId);
    }

    [Fact]
    public async Task UpdateEventCommandHandler_Should_LeaveTitleUnchanged_When_OnlyDescriptionAndCategoryPatched()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|update-partial", "partial@example.com");
        var eventId = await SeedDraftEventAsync(databaseName, identity, "Keep This Title");
        var categoryId = await SeedCategoryAsync(databaseName);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var command = new UpdateEventCommand(
            eventId,
            Description: "Screen 2 description",
            CategoryId: categoryId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Keep This Title", result.Value.Title);
        Assert.Equal("Screen 2 description", result.Value.Description);
        Assert.Equal(categoryId, result.Value.CategoryId);
        Assert.Equal("Music", result.Value.CategoryName);
    }

    [Fact]
    public async Task UpdateEventCommandHandler_Should_ReplaceAllLocations_When_LocationsProvided()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|update-locations", "locations@example.com");
        var eventId = await SeedDraftEventAsync(databaseName, identity, "Location Event");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var segmentStart = new DateTime(2026, 8, 1, 18, 0, 0, DateTimeKind.Utc);
        var segmentEnd = new DateTime(2026, 8, 1, 20, 0, 0, DateTimeKind.Utc);
        var locations = new List<EventLocationResponse>
        {
            new("Hall A", segmentStart, segmentEnd, EventLocationKind.Physical, null, "1 Main St", null, null, "Sofia", "BG", null),
            new("Live Stream", null, null, EventLocationKind.Virtual, "https://stream.example.com", null, null, null, null, null, null)
        };

        var command = new UpdateEventCommand(eventId, Locations: locations);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Locations.Count);
        Assert.Equal(EventLocationType.Hybrid, result.Value.LocationType);

        var physical = result.Value.Locations.Single(l => l.Name == "Hall A");
        Assert.Equal(segmentStart, physical.StartsAt);
        Assert.Equal(segmentEnd, physical.EndsAt);

        var stream = result.Value.Locations.Single(l => l.Name == "Live Stream");
        Assert.Null(stream.StartsAt);
        Assert.Null(stream.EndsAt);
    }

    [Fact]
    public async Task UpdateEventCommandHandler_Should_ReturnSegmentTimeOutOfBounds_When_SegmentOutsideEventWindow()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|segment-oob", "oob@example.com");
        var eventId = await SeedDraftEventAsync(databaseName, identity, "Bounds Event");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var eventStart = new DateTime(2026, 7, 1, 18, 0, 0, DateTimeKind.Utc);
        var eventEnd = new DateTime(2026, 7, 1, 22, 0, 0, DateTimeKind.Utc);

        await handler.Handle(
            new UpdateEventCommand(eventId, StartTime: eventStart, EndTime: eventEnd, TimeZoneId: "Europe/Sofia"),
            CancellationToken.None);

        var command = new UpdateEventCommand(
            eventId,
            Locations:
            [
                new EventLocationResponse(
                    "Hall",
                    eventStart.AddHours(-1),
                    eventEnd,
                    EventLocationKind.Physical,
                    null,
                    "1 Main St",
                    null,
                    null,
                    "Sofia",
                    null,
                    null)
            ]);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.SegmentTimeOutOfBounds", result.Error.Code);
    }

    [Fact]
    public async Task UpdateEventCommandHandler_Should_ReturnSegmentTimesIncomplete_When_OnlyStartsAtSet()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|segment-incomplete", "incomplete@example.com");
        var eventId = await SeedDraftEventAsync(databaseName, identity, "Incomplete Segment");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var eventStart = new DateTime(2026, 7, 1, 18, 0, 0, DateTimeKind.Utc);
        var command = new UpdateEventCommand(
            eventId,
            Locations:
            [
                new EventLocationResponse(
                    "Hall",
                    eventStart,
                    null,
                    EventLocationKind.Physical,
                    null,
                    "1 Main St",
                    null,
                    null,
                    "Sofia",
                    null,
                    null)
            ]);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.SegmentTimesIncomplete", result.Error.Code);
    }

    [Fact]
    public async Task UpdateEventCommandHandler_Should_ReturnInvalidSegmentTimeRange_When_StartsAtNotBeforeEndsAt()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|segment-range", "range@example.com");
        var eventId = await SeedDraftEventAsync(databaseName, identity, "Bad Segment Range");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var eventStart = new DateTime(2026, 7, 1, 18, 0, 0, DateTimeKind.Utc);
        var eventEnd = new DateTime(2026, 7, 1, 22, 0, 0, DateTimeKind.Utc);

        await handler.Handle(
            new UpdateEventCommand(eventId, StartTime: eventStart, EndTime: eventEnd, TimeZoneId: "Europe/Sofia"),
            CancellationToken.None);

        var command = new UpdateEventCommand(
            eventId,
            Locations:
            [
                new EventLocationResponse(
                    "Hall",
                    eventEnd,
                    eventStart,
                    EventLocationKind.Physical,
                    null,
                    "1 Main St",
                    null,
                    null,
                    "Sofia",
                    null,
                    null)
            ]);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.InvalidSegmentTimeRange", result.Error.Code);
    }

    [Fact]
    public async Task UpdateEventCommandHandler_Should_RejectEventTimePatch_When_ExistingSegmentsFallOutsideNewBounds()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|segment-shrink", "shrink@example.com");
        var eventId = await SeedDraftEventAsync(databaseName, identity, "Shrink Event");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var eventStart = new DateTime(2026, 7, 1, 18, 0, 0, DateTimeKind.Utc);
        var eventEnd = new DateTime(2026, 7, 1, 22, 0, 0, DateTimeKind.Utc);

        await handler.Handle(
            new UpdateEventCommand(
                eventId,
                StartTime: eventStart,
                EndTime: eventEnd,
                TimeZoneId: "Europe/Sofia",
                Locations:
                [
                    new EventLocationResponse(
                        "Hall",
                        eventStart,
                        eventEnd,
                        EventLocationKind.Physical,
                        null,
                        "1 Main St",
                        null,
                        null,
                        "Sofia",
                        null,
                        null)
                ]),
            CancellationToken.None);

        var command = new UpdateEventCommand(eventId, EndTime: eventStart.AddHours(1));

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.SegmentTimeOutOfBounds", result.Error.Code);
    }

    [Fact]
    public async Task UpdateEventCommandHandler_Should_AcceptPhysicalSegment_When_OnlyCoordinatesProvided()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|segment-coords", "coords@example.com");
        var eventId = await SeedDraftEventAsync(databaseName, identity, "Coords Event");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var command = new UpdateEventCommand(
            eventId,
            Locations:
            [
                new EventLocationResponse(
                    "Map Pin",
                    null,
                    null,
                    EventLocationKind.Physical,
                    null,
                    null,
                    42.6977,
                    23.3219,
                    null,
                    null,
                    null)
            ]);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(42.6977, result.Value.Locations[0].Latitude);
        Assert.Equal(23.3219, result.Value.Locations[0].Longitude);
    }

    [Fact]
    public async Task UpdateEventCommandHandler_Should_ReturnCategoryNotFound_When_CategoryIdDoesNotExist()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|update-bad-category", "badcat@example.com");
        var eventId = await SeedDraftEventAsync(databaseName, identity, "Category Test");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var missingCategoryId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var command = new UpdateEventCommand(eventId, CategoryId: missingCategoryId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.CategoryNotFound", result.Error.Code);
    }

    [Fact]
    public async Task UpdateEventCommandHandler_Should_ReturnInsufficientPermissions_When_NonEditorPatches()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var ownerIdentity = CreateVerifiedIdentity("auth0|update-owner", "owner@example.com");
        var eventId = await SeedDraftEventAsync(databaseName, ownerIdentity, "Private Draft");

        var strangerIdentity = CreateVerifiedIdentity("auth0|update-stranger", "stranger@example.com");
        await using var context = CreateContext(databaseName);
        var strangerHandler = CreateHandler(context, strangerIdentity);

        var command = new UpdateEventCommand(eventId, Title: "Hijacked");

        // Act
        var result = await strangerHandler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.InsufficientPermissions", result.Error.Code);
    }

    [Fact]
    public async Task UpdateEventCommandHandler_Should_ReturnNotFound_When_EventIsSoftDeleted()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|update-deleted", "deleted@example.com");
        var eventId = await SeedDraftEventAsync(databaseName, identity, "Deleted Event", softDeleted: true);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var command = new UpdateEventCommand(eventId, Title: "Too Late");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.Deleted", result.Error.Code);
    }

    [Fact]
    public async Task UpdateEventCommandHandler_Should_ReturnCannotModifyCancelled_When_EventIsCancelled()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|update-cancelled", "cancelled@example.com");
        var eventId = await SeedPublishedEventAsync(databaseName, identity, cancelled: true);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var command = new UpdateEventCommand(eventId, Title: "Revive");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.CannotModifyCancelled", result.Error.Code);
    }

    [Fact]
    public async Task UpdateEventCommandHandler_Should_UpdatePublishedEvent_When_CreatorPatches()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|update-published", "published@example.com");
        var eventId = await SeedPublishedEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var command = new UpdateEventCommand(eventId, Title: "Updated Published Title");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(EventStatus.Published, result.Value.Status);
        Assert.Equal("Updated Published Title", result.Value.Title);
    }

    [Fact]
    public async Task UpdateEventCommandHandler_Should_ReturnVenueConflict_When_PublishedLocationPatchSharesVenue()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|update-venue-conflict", "upvenue@example.com");
        var start = new DateTime(2026, 7, 1, 18, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 7, 1, 22, 0, 0, DateTimeKind.Utc);
        await SeedPublishedEventWithVenueAsync(
            databaseName,
            identity,
            "Occupant",
            "123 Main St",
            "Sofia",
            start,
            end);
        var eventId = await SeedPublishedEventWithVenueAsync(
            databaseName,
            identity,
            "Moving Event",
            "456 Other St",
            "Plovdiv",
            start,
            end);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var command = new UpdateEventCommand(
            eventId,
            Locations:
            [
                new EventLocationResponse(
                    "Main Hall",
                    null,
                    null,
                    EventLocationKind.Physical,
                    null,
                    "123 Main St",
                    null,
                    null,
                    "Sofia",
                    null,
                    null)
            ]);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.VenueConflict", result.Error.Code);
    }

    [Fact]
    public async Task UpdateEventCommandHandler_Should_UpdateDraftLocations_When_PublishedOccupantExists()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|update-draft-venue", "draftvenue@example.com");
        var start = new DateTime(2026, 7, 1, 18, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 7, 1, 22, 0, 0, DateTimeKind.Utc);
        await SeedPublishedEventWithVenueAsync(
            databaseName,
            identity,
            "Occupant",
            "123 Main St",
            "Sofia",
            start,
            end);
        var eventId = await SeedDraftEventAsync(databaseName, identity, "Draft Event");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var command = new UpdateEventCommand(
            eventId,
            StartTime: start,
            EndTime: end,
            TimeZoneId: "Europe/Sofia",
            Locations:
            [
                new EventLocationResponse(
                    "Main Hall",
                    null,
                    null,
                    EventLocationKind.Physical,
                    null,
                    "123 Main St",
                    null,
                    null,
                    "Sofia",
                    null,
                    null)
            ]);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task UpdateEventCommandHandler_Should_ReturnVenueConflict_When_PublishedTimePatchCreatesOverlap()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|update-time-venue", "timvenue@example.com");
        var occupantStart = new DateTime(2026, 7, 1, 18, 0, 0, DateTimeKind.Utc);
        var occupantEnd = new DateTime(2026, 7, 1, 22, 0, 0, DateTimeKind.Utc);
        await SeedPublishedEventWithVenueAsync(
            databaseName,
            identity,
            "Occupant",
            "123 Main St",
            "Sofia",
            occupantStart,
            occupantEnd);
        var eventId = await SeedPublishedEventWithVenueAsync(
            databaseName,
            identity,
            "Later Event",
            "123 Main St",
            "Sofia",
            new DateTime(2026, 7, 2, 18, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 7, 2, 22, 0, 0, DateTimeKind.Utc));
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var command = new UpdateEventCommand(
            eventId,
            StartTime: occupantStart,
            EndTime: occupantEnd);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.VenueConflict", result.Error.Code);
    }

    [Fact]
    public async Task UpdateEventCommandHandler_Should_UpdatePublishedLocations_When_SameVenueOnSelf()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|update-self-venue", "selfvenue@example.com");
        var start = new DateTime(2026, 7, 1, 18, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 7, 1, 22, 0, 0, DateTimeKind.Utc);
        var eventId = await SeedPublishedEventWithVenueAsync(
            databaseName,
            identity,
            "Self Event",
            "123 Main St",
            "Sofia",
            start,
            end);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var command = new UpdateEventCommand(
            eventId,
            Locations:
            [
                new EventLocationResponse(
                    "Renamed Hall",
                    null,
                    null,
                    EventLocationKind.Physical,
                    null,
                    "123 Main St",
                    null,
                    null,
                    "Sofia",
                    null,
                    null)
            ]);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Renamed Hall", result.Value.Locations[0].Name);
    }

    [Fact]
    public async Task UpdateEventCommandHandler_Should_UpdateGroupHostedEvent_When_OrganizerPatches()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|org-owner", "owner@org.example.com");
        var organizer = CreateUser("auth0|org-organizer", "organizer@org.example.com");
        var group = SeedGroupWithMember(databaseName, owner, organizer, GroupMemberRole.Organizer);
        var eventId = await SeedDraftEventForHostAsync(
            databaseName,
            CreateVerifiedIdentity("auth0|org-organizer", "organizer@org.example.com"),
            group.Id,
            "Org Event");

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(
            context,
            CreateVerifiedIdentity("auth0|org-organizer", "organizer@org.example.com"));

        var command = new UpdateEventCommand(eventId, Description: "Organizer edit");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Organizer edit", result.Value.Description);
        Assert.True(result.Value.HostIsGroup);
        Assert.Equal(group.Id, result.Value.HostParticipantId);
    }

    [Fact]
    public async Task UpdateEventCommandHandler_Should_ReturnInsufficientPermissions_When_GroupMemberWithoutOrganizerRolePatches()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|member-owner", "owner@member.org");
        var member = CreateUser("auth0|plain-member", "member@org.example.com");
        var group = SeedGroupWithMember(databaseName, owner, member, GroupMemberRole.Member);
        var eventId = await SeedDraftEventForHostAsync(
            databaseName,
            CreateVerifiedIdentity("auth0|member-owner", "owner@member.org"),
            group.Id,
            "Members Cannot Edit");

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(
            context,
            CreateVerifiedIdentity("auth0|plain-member", "member@org.example.com"));

        var command = new UpdateEventCommand(eventId, Description: "Member attempt");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.InsufficientPermissions", result.Error.Code);
    }

    [Fact]
    public async Task UpdateEventCommandHandler_Should_ReturnInsufficientPermissions_When_FormerGroupOrganizerPatches()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|former-patch-owner", "fpatch-owner@example.com");
        var formerOrganizer = CreateUser("auth0|former-patch-org", "fpatch-org@example.com");
        var group = SeedGroupWithMember(databaseName, owner, formerOrganizer, GroupMemberRole.Organizer);

        var formerOrganizerIdentity = CreateVerifiedIdentity("auth0|former-patch-org", "fpatch-org@example.com");
        var eventId = await SeedDraftEventForHostAsync(
            databaseName,
            formerOrganizerIdentity,
            group.Id,
            "Former Organizer Draft");

        await using (var seedContext = CreateContext(databaseName))
        {
            seedContext.GroupMemberships.RemoveRange(
                seedContext.GroupMemberships.Where(m => m.UserId == formerOrganizer.Id && m.GroupId == group.Id));
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, formerOrganizerIdentity);

        var command = new UpdateEventCommand(eventId, Description: "Former member attempt");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.InsufficientPermissions", result.Error.Code);
    }

    [Fact]
    public async Task UpdateEventCommandHandler_Should_ReturnNoFieldsToUpdate_When_CommandIsEmpty()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|update-empty", "empty@example.com");
        var eventId = await SeedDraftEventAsync(databaseName, identity, "Empty Patch");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var command = new UpdateEventCommand(eventId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.NoFieldsToUpdate", result.Error.Code);
    }

    [Fact]
    public async Task UpdateEventCommandHandler_Should_ClearAttachedPlugins_When_DraftTierDowngradedToSmall()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|tier-downgrade", "tier@example.com");
        var eventId = await SeedBigDraftEventWithPluginAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);
        var command = new UpdateEventCommand(eventId, Tier: EventTier.Small);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(EventTier.Small, result.Value.Tier);

        await using var verifyContext = CreateContext(databaseName);
        var persisted = await verifyContext.Events
            .Include(e => e.Plugins)
            .SingleAsync(e => e.Id == eventId);
        Assert.Empty(persisted.Plugins);
    }

    [Fact]
    public async Task UpdateEventCommandHandler_Should_ReturnEmailNotVerified_When_EmailIsUnverified()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = new FakeUserIdentityAccessor
        {
            IsAuthenticated = true,
            ExternalSubjectId = "auth0|update-unverified",
            Email = "unverified@example.com",
            EmailVerified = false,
            ServiceRole = ServiceRole.User
        };

        var verifiedIdentity = CreateVerifiedIdentity("auth0|update-unverified-host", "host@example.com");
        var eventId = await SeedDraftEventAsync(databaseName, verifiedIdentity, "Gate Test");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var command = new UpdateEventCommand(eventId, Title: "Blocked");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Users.EmailNotVerified", result.Error.Code);
    }

    [Fact]
    public async Task UpdateEventCommandHandler_Should_ReturnCannotSwitchToPaidWithRsvps_When_DraftHasAttendees()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|admission-free-rsvp", "free-rsvp@example.com");
        var eventId = await SeedDraftEventWithFreeAdmissionAndAttendeeAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var command = new UpdateEventCommand(eventId, AdmissionType: AdmissionType.Paid);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(EventErrors.CannotSwitchToPaidWithRsvps.Code, result.Error.Code);
    }

    [Fact]
    public async Task UpdateEventCommandHandler_Should_RemoveTicketTypes_When_DraftPaidSwitchesToFree()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|admission-paid-free", "paid-free@example.com");
        var eventId = await SeedDraftEventWithPaidAdmissionAndTicketTypeAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var command = new UpdateEventCommand(eventId, AdmissionType: AdmissionType.Free);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(AdmissionType.Free, result.Value.AdmissionType);

        await using var verifyContext = CreateContext(databaseName);
        Assert.False(await verifyContext.TicketTypes.AnyAsync(t => t.EventId == eventId));
    }

    [Fact]
    public async Task UpdateEventCommandHandler_Should_ReturnCannotSwitchToFreeWithTicketSales_When_SoldQuantityPositive()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|admission-sales-block", "sales-block@example.com");
        var eventId = await SeedDraftEventWithPaidAdmissionAndSoldTicketTypeAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        var command = new UpdateEventCommand(eventId, AdmissionType: AdmissionType.Free);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(EventErrors.CannotSwitchToFreeWithTicketSales.Code, result.Error.Code);
    }

    private static async Task<Guid> SeedDraftEventWithFreeAdmissionAndAttendeeAsync(
        string databaseName,
        FakeUserIdentityAccessor identity)
    {
        await using var context = CreateContext(databaseName);
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var user = (await currentUserService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var createResult = EventService.Create(EventTier.Small, "Free With RSVP", user.Id);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;
        var categoryId = SeedCategoryInContext(context);

        MakePublishReadyViaUpdate(@event, categoryId, user.Id);

        var attendee = EventAttendee.Create(@event.Id, Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"));
        attendee.Status = EventAttendeeStatus.Going;
        attendee.RegisteredAt = DateTime.UtcNow;
        @event.Attendees.Add(attendee);

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
        return @event.Id;
    }

    private static async Task<Guid> SeedDraftEventWithPaidAdmissionAndTicketTypeAsync(
        string databaseName,
        FakeUserIdentityAccessor identity)
    {
        await using var context = CreateContext(databaseName);
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var user = (await currentUserService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var createResult = EventService.Create(EventTier.Small, "Paid Draft", user.Id);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;
        @event.AdmissionType = AdmissionType.Paid;
        TicketTypeService.Create(@event, "General", "Entry", priceCents: 2500, capacity: 100);

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
        return @event.Id;
    }

    private static async Task<Guid> SeedDraftEventWithPaidAdmissionAndSoldTicketTypeAsync(
        string databaseName,
        FakeUserIdentityAccessor identity)
    {
        await using var context = CreateContext(databaseName);
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var user = (await currentUserService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var createResult = EventService.Create(EventTier.Small, "Paid With Sales", user.Id);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;
        @event.AdmissionType = AdmissionType.Paid;
        var ticketType = TicketTypeService.Create(@event, "General", "Entry", priceCents: 2500, capacity: 100).Value;
        ticketType.SoldQuantity = 1;

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
        return @event.Id;
    }

    private static async Task<Guid> SeedBigDraftEventWithPluginAsync(
        string databaseName,
        FakeUserIdentityAccessor identity)
    {
        await using var context = CreateContext(databaseName);
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var user = (await currentUserService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var createResult = EventService.Create(EventTier.Big, "Big With Plugin", user.Id);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;

        var plugin = new Plugin
        {
            Code = PluginConstants.CodeFaq,
            Name = "FAQ",
            Description = "FAQ plugin",
            Version = "1.0.0"
        };
        context.Plugins.Add(plugin);

        var entries = System.Text.Json.JsonSerializer.Serialize(new[]
        {
            new { question = "Q?", answer = "A." }
        });
        var data = new Dictionary<string, string?> { [PluginConstants.DataKeyEntries] = entries };
        PluginService.Attach(@event, plugin, data);

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
        return @event.Id;
    }

    private static async Task<Guid> SeedDraftEventAsync(
        string databaseName,
        FakeUserIdentityAccessor identity,
        string title,
        bool softDeleted = false)
    {
        await using var context = CreateContext(databaseName);
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var user = (await currentUserService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var createResult = EventService.Create(EventTier.Small, title, user.Id);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;

        if (softDeleted)
        {
            EventService.SoftDelete(@event);
        }

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
        return @event.Id;
    }

    private static async Task<Guid> SeedDraftEventForHostAsync(
        string databaseName,
        FakeUserIdentityAccessor creatorIdentity,
        Guid hostParticipantId,
        string title)
    {
        await using var context = CreateContext(databaseName);
        var currentUserService = new CurrentUserService(
            context,
            creatorIdentity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        await currentUserService.GetOrProvisionAsync(CancellationToken.None);

        var createResult = EventService.Create(EventTier.Small, title, hostParticipantId);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
        return @event.Id;
    }

    private static async Task<Guid> SeedPublishedEventWithVenueAsync(
        string databaseName,
        FakeUserIdentityAccessor identity,
        string title,
        string address,
        string city,
        DateTime start,
        DateTime end)
    {
        await using var context = CreateContext(databaseName);
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var user = (await currentUserService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var createResult = EventService.Create(EventTier.Small, title, user.Id);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;

        var categoryId = SeedCategoryInContext(context);
        var patch = new EventUpdatePatch
        {
            Description = "A test event description",
            CategoryId = categoryId,
            StartTime = start,
            EndTime = end,
            TimeZoneId = "Europe/Sofia",
            AdmissionType = AdmissionType.Free,
            Locations =
            [
                new EventLocation
                {
                    Name = "Main Hall",
                    Kind = EventLocationKind.Physical,
                    Address = address,
                    City = city
                }
            ]
        };

        var updateResult = EventService.Update(@event, patch, user.Id);
        if (updateResult.IsFailure)
        {
            throw new InvalidOperationException(updateResult.Error.Code);
        }

        EventService.Publish(@event, categoryExists: true, recentPublishCount: 0, maxPublishesPerWeek: 6);

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
        return @event.Id;
    }

    private static async Task<Guid> SeedPublishedEventAsync(
        string databaseName,
        FakeUserIdentityAccessor identity,
        bool cancelled = false)
    {
        await using var context = CreateContext(databaseName);
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var user = (await currentUserService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var createResult = EventService.Create(EventTier.Small, "Published Event", user.Id);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;

        var categoryId = SeedCategoryInContext(context);
        MakePublishReadyViaUpdate(@event, categoryId, user.Id);
        EventService.Publish(@event, categoryExists: true, recentPublishCount: 0, maxPublishesPerWeek: 6);

        if (cancelled)
        {
            EventService.Cancel(@event);
        }

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
        return @event.Id;
    }

    private static async Task<Guid> SeedCategoryAsync(string databaseName)
    {
        await using var context = CreateContext(databaseName);
        var categoryId = SeedCategoryInContext(context);
        await context.SaveChangesAsync(CancellationToken.None);
        return categoryId;
    }

    private static Guid SeedCategoryInContext(ApplicationDbContext context)
    {
        var existing = context.EventCategories.FirstOrDefault();
        if (existing is not null)
        {
            return existing.Id;
        }

        var category = new EventCategory { Name = "Music" };
        context.EventCategories.Add(category);
        return category.Id;
    }

    private static void MakePublishReadyViaUpdate(Event @event, Guid categoryId, Guid actingUserId)
    {
        var start = new DateTime(2026, 7, 1, 18, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 7, 1, 22, 0, 0, DateTimeKind.Utc);
        var patch = new EventUpdatePatch
        {
            Description = "A test event description",
            CategoryId = categoryId,
            StartTime = start,
            EndTime = end,
            TimeZoneId = "Europe/Sofia",
            AdmissionType = AdmissionType.Free,
            Locations =
            [
                new EventLocation
                {
                    Name = "Main Hall",
                    Kind = EventLocationKind.Physical,
                    Address = "123 Main St",
                    City = "Sofia"
                }
            ]
        };

        var updateResult = EventService.Update(@event, patch, actingUserId);
        if (updateResult.IsFailure)
        {
            throw new InvalidOperationException(updateResult.Error.Code);
        }
    }

    private static Group SeedGroupWithMember(
        string databaseName,
        User owner,
        User member,
        GroupMemberRole memberRole)
    {
        var createResult = GroupService.Create(
            "Host Org",
            "",
            GroupJoinPolicy.Open,
            owner.Id,
            DefaultGroupImageUrl);
        var group = createResult.Value.Group;
        var membership = GroupMembership.Create(group.Id, member.Id, memberRole);

        using var seedContext = CreateContext(databaseName);
        seedContext.Users.AddRange(owner, member);
        seedContext.Groups.Add(group);
        seedContext.GroupMemberships.Add(createResult.Value.OwnerMembership);
        if (member.Id != owner.Id)
        {
            seedContext.GroupMemberships.Add(membership);
        }

        seedContext.SaveChanges();
        return group;
    }

    private static User CreateUser(string subject, string email) =>
        UserService.ProvisionFromExternalIdentity(
            subject,
            email,
            emailVerified: true,
            profilePictureUrl: null,
            DefaultAvatarUrl,
            ServiceRole.User);

    private static FakeUserIdentityAccessor CreateVerifiedIdentity(string externalSubjectId, string email) =>
        new()
        {
            IsAuthenticated = true,
            ExternalSubjectId = externalSubjectId,
            Email = email,
            EmailVerified = true,
            ServiceRole = ServiceRole.User
        };

    private static UpdateEventCommandHandler CreateHandler(
        ApplicationDbContext context,
        IUserIdentityAccessor identity)
    {
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));

        return new UpdateEventCommandHandler(
            context,
            currentUserService,
            identity,
            new EventAccessService(context),
            new EventVenueConflictService(context));
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
