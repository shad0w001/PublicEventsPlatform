using Application.Abstractions.Authentication;
using Application.Events;
using Application.Events.PublishEvent;
using Domain.Tickets.Services;
using Application.Events.Services;
using Application.Events.UpdateEvent;
using Application.Users;
using Application.Users.Services;
using Domain.Events;
using Domain.Events.EventLocations;
using Domain.Events.Services;
using Domain.Groups;
using Domain.Groups.Services;
using Domain.Users;
using Domain.Users.Services;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApplicationTests.Events.PublishEvent;

public class PublishEventCommandHandlerTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";
    private const string DefaultGroupImageUrl = "/images/default-group.png";
    private const int MaxPublishesPerWeek = 6;

    [Fact]
    public async Task PublishEventCommandHandler_Should_PublishCompleteDraft_When_AllFieldsSet()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|publish-success", "publish@example.com");
        var eventId = await SeedPublishReadyDraftAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new PublishEventCommand(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(EventStatus.Published, result.Value.Status);
        Assert.NotNull(result.Value.PublishedAt);
        Assert.Equal("Music", result.Value.CategoryName);
    }

    [Fact]
    public async Task PublishEventCommandHandler_Should_ReturnPaidPublishRequiresTicketTypes_When_PaidDraftHasNoTicketTypes()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|publish-paid-no-types", "paid-no-types@example.com");
        var eventId = await SeedPublishReadyDraftAsync(
            databaseName,
            identity,
            admissionType: AdmissionType.Paid,
            seedTicketType: false);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new PublishEventCommand(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(EventErrors.PaidPublishRequiresTicketTypes.Code, result.Error.Code);
    }

    [Fact]
    public async Task PublishEventCommandHandler_Should_Publish_When_AdmissionTypeIsPaid()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|publish-paid", "paid@example.com");
        var eventId = await SeedPublishReadyDraftAsync(
            databaseName,
            identity,
            admissionType: AdmissionType.Paid);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new PublishEventCommand(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(AdmissionType.Paid, result.Value.AdmissionType);
    }

    [Fact]
    public async Task PublishEventCommandHandler_Should_AddHostGoingAttendee_When_FreeEventIsPublished()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|publish-host-rsvp", "host-rsvp@example.com");
        var eventId = await SeedPublishReadyDraftAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);
        var user = await context.Users.SingleAsync();

        // Act
        var result = await handler.Handle(new PublishEventCommand(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        await using var verifyContext = CreateContext(databaseName);
        var attendee = await verifyContext.EventAttendees.SingleAsync();
        Assert.Equal(user.Id, attendee.ParticipantId);
        Assert.Equal(EventAttendeeStatus.Going, attendee.Status);
        Assert.NotNull(attendee.RegisteredAt);
    }

    [Fact]
    public async Task PublishEventCommandHandler_Should_NotAddAttendee_When_PaidEventIsPublished()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|publish-paid-no-rsvp", "paid-no-rsvp@example.com");
        var eventId = await SeedPublishReadyDraftAsync(
            databaseName,
            identity,
            admissionType: AdmissionType.Paid);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new PublishEventCommand(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(await context.EventAttendees.ToListAsync());
    }

    [Fact]
    public async Task PublishEventCommandHandler_Should_AddGroupHostGoingAttendee_When_GroupHostsFreeEvent()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|pub-rsvp-org-owner", "rsvp-owner@org.example.com");
        var organizer = CreateUser("auth0|pub-rsvp-org-organizer", "rsvp-organizer@org.example.com");
        var group = SeedGroupWithMember(databaseName, owner, organizer, GroupMemberRole.Organizer);
        var organizerIdentity = CreateVerifiedIdentity("auth0|pub-rsvp-org-organizer", "rsvp-organizer@org.example.com");
        var eventId = await SeedPublishReadyDraftForHostAsync(
            databaseName,
            organizerIdentity,
            group.Id,
            "Group RSVP Publish");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, organizerIdentity);

        // Act
        var result = await handler.Handle(new PublishEventCommand(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        await using var verifyContext = CreateContext(databaseName);
        var attendee = await verifyContext.EventAttendees.SingleAsync();
        Assert.Equal(group.Id, attendee.ParticipantId);
        Assert.Equal(EventAttendeeStatus.Going, attendee.Status);
        Assert.NotNull(attendee.RegisteredAt);
    }

    [Fact]
    public async Task PublishEventCommandHandler_Should_Publish_When_SegmentTimesNull()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|publish-null-segments", "nullseg@example.com");
        var eventId = await SeedPublishReadyDraftAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new PublishEventCommand(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Locations);
        Assert.Null(result.Value.Locations[0].StartsAt);
        Assert.Null(result.Value.Locations[0].EndsAt);
    }

    [Fact]
    public async Task PublishEventCommandHandler_Should_ReturnInvalidSegmentTimeRange_When_SegmentTimesInvalidAtPublish()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|publish-bad-segment", "badseg@example.com");
        var eventId = await SeedPublishReadyDraftWithInvalidSegmentAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new PublishEventCommand(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.InvalidSegmentTimeRange", result.Error.Code);
    }

    [Fact]
    public async Task PublishEventCommandHandler_Should_ReturnValidationError_When_DraftIsIncomplete()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|publish-incomplete", "incomplete@example.com");
        var eventId = await SeedDraftEventAsync(databaseName, identity, "Incomplete");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new PublishEventCommand(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.InvalidDescription", result.Error.Code);
    }

    [Fact]
    public async Task PublishEventCommandHandler_Should_ReturnVenueConflict_When_PublishedEventSharesPhysicalVenue()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|publish-venue-conflict", "venueconflict@example.com");
        await SeedPublishedEventAsync(databaseName, identity);
        var eventId = await SeedPublishReadyDraftAsync(databaseName, identity, title: "Overlapping Venue");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new PublishEventCommand(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.VenueConflict", result.Error.Code);
    }

    [Fact]
    public async Task PublishEventCommandHandler_Should_Publish_When_CancelledEventOccupiedSameVenue()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|publish-cancelled-venue", "cancelledvenue@example.com");
        await SeedRecentPublishedEventsAsync(databaseName, identity, count: 1, cancelled: true);
        var eventId = await SeedPublishReadyDraftAsync(databaseName, identity, title: "After Cancelled");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new PublishEventCommand(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task PublishEventCommandHandler_Should_Publish_When_ExistingDraftSharesSameVenue()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|publish-draft-venue", "draftvenue@example.com");
        await SeedPublishReadyDraftAsync(databaseName, identity, title: "Draft Occupant");
        var eventId = await SeedPublishReadyDraftAsync(databaseName, identity, title: "Publishing Draft");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new PublishEventCommand(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task PublishEventCommandHandler_Should_Publish_When_OnlyVirtualOccupantOverlaps()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|publish-virtual-occupant", "virtualocc@example.com");
        await SeedPublishedVirtualOnlyEventAsync(databaseName, identity);
        var eventId = await SeedPublishReadyDraftAsync(databaseName, identity, title: "Physical New Event");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new PublishEventCommand(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task PublishEventCommandHandler_Should_ReturnAlreadyPublished_When_EventIsPublished()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|publish-twice", "twice@example.com");
        var eventId = await SeedPublishedEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new PublishEventCommand(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.AlreadyPublished", result.Error.Code);
    }

    [Fact]
    public async Task PublishEventCommandHandler_Should_ReturnPublishRateLimitExceeded_When_HostAtLimit()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|publish-rate-limit", "limit@example.com");
        await SeedRecentPublishedEventsAsync(databaseName, identity, count: MaxPublishesPerWeek);
        var eventId = await SeedPublishReadyDraftAsync(databaseName, identity, title: "One Too Many");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new PublishEventCommand(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.PublishRateLimitExceeded", result.Error.Code);
    }

    [Fact]
    public async Task PublishEventCommandHandler_Should_NotApplyUserRateLimit_When_GroupHostHasSeparateQuota()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|group-owner", "owner@group.example.com");
        var organizer = CreateUser("auth0|group-organizer", "org@group.example.com");
        var group = SeedGroupWithMember(databaseName, owner, organizer, GroupMemberRole.Organizer);

        var userIdentity = CreateVerifiedIdentity("auth0|group-organizer", "org@group.example.com");
        await SeedRecentPublishedEventsAsync(databaseName, userIdentity, count: MaxPublishesPerWeek);

        var eventId = await SeedPublishReadyDraftForHostAsync(
            databaseName,
            userIdentity,
            group.Id,
            "Org Event");

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, userIdentity);

        // Act
        var result = await handler.Handle(new PublishEventCommand(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(EventStatus.Published, result.Value.Status);
        Assert.True(result.Value.HostIsGroup);
    }

    [Fact]
    public async Task PublishEventCommandHandler_Should_ReturnInsufficientPermissions_When_NonEditorPublishes()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var ownerIdentity = CreateVerifiedIdentity("auth0|publish-owner", "owner@example.com");
        var eventId = await SeedPublishReadyDraftAsync(databaseName, ownerIdentity);

        var strangerIdentity = CreateVerifiedIdentity("auth0|publish-stranger", "stranger@example.com");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, strangerIdentity);

        // Act
        var result = await handler.Handle(new PublishEventCommand(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.InsufficientPermissions", result.Error.Code);
    }

    [Fact]
    public async Task PublishEventCommandHandler_Should_ReturnNotFound_When_EventIsSoftDeleted()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|publish-deleted", "deleted@example.com");
        var eventId = await SeedPublishReadyDraftAsync(databaseName, identity, softDeleted: true);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new PublishEventCommand(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.Deleted", result.Error.Code);
    }

    [Fact]
    public async Task PublishEventCommandHandler_Should_ReturnEmailNotVerified_When_EmailIsUnverified()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = new FakeUserIdentityAccessor
        {
            IsAuthenticated = true,
            ExternalSubjectId = "auth0|publish-unverified",
            Email = "unverified@example.com",
            EmailVerified = false,
            ServiceRole = ServiceRole.User
        };

        var hostIdentity = CreateVerifiedIdentity("auth0|publish-unverified-host", "host@example.com");
        var eventId = await SeedPublishReadyDraftAsync(databaseName, hostIdentity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new PublishEventCommand(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Users.EmailNotVerified", result.Error.Code);
    }

    [Fact]
    public async Task PublishEventCommandHandler_Should_PublishGroupHostedEvent_When_OrganizerPublishes()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|pub-org-owner", "owner@org.example.com");
        var organizer = CreateUser("auth0|pub-org-organizer", "organizer@org.example.com");
        var group = SeedGroupWithMember(databaseName, owner, organizer, GroupMemberRole.Organizer);

        var organizerIdentity = CreateVerifiedIdentity("auth0|pub-org-organizer", "organizer@org.example.com");
        var eventId = await SeedPublishReadyDraftForHostAsync(
            databaseName,
            organizerIdentity,
            group.Id,
            "Org Publish");

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, organizerIdentity);

        // Act
        var result = await handler.Handle(new PublishEventCommand(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(group.Id, result.Value.HostParticipantId);
        Assert.Equal(EventStatus.Published, result.Value.Status);
    }

    [Fact]
    public async Task PublishEventCommandHandler_Should_CountCancelledEventTowardRateLimit_When_PriorPublishExists()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|publish-cancelled-count", "cancelled@example.com");
        await SeedRecentPublishedEventsAsync(databaseName, identity, count: MaxPublishesPerWeek, cancelled: true);
        var eventId = await SeedPublishReadyDraftAsync(databaseName, identity, title: "Should Hit Limit");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act — six prior publishes (cancelled) already at limit
        var result = await handler.Handle(new PublishEventCommand(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.PublishRateLimitExceeded", result.Error.Code);
    }

    private static async Task<Guid> SeedPublishReadyDraftAsync(
        string databaseName,
        FakeUserIdentityAccessor identity,
        string title = "Publish Ready",
        AdmissionType admissionType = AdmissionType.Free,
        bool softDeleted = false,
        bool seedTicketType = true)
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
        MakePublishReadyViaUpdate(@event, categoryId, user.Id, admissionType);

        if (admissionType == AdmissionType.Paid && seedTicketType)
        {
            TicketTypeService.Create(@event, "General Admission", "Standard entry", priceCents: 2500, capacity: 100);
        }

        if (softDeleted)
        {
            EventService.SoftDelete(@event);
        }

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
        return @event.Id;
    }

    private static async Task<Guid> SeedPublishReadyDraftWithInvalidSegmentAsync(
        string databaseName,
        FakeUserIdentityAccessor identity)
    {
        await using var context = CreateContext(databaseName);
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var user = (await currentUserService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var createResult = EventService.Create(EventTier.Small, "Bad Segment Publish", user.Id);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;

        var categoryId = SeedCategoryInContext(context);
        MakePublishReadyViaUpdate(@event, categoryId, user.Id);
        @event.Locations[0].StartsAt = @event.EndTime;
        @event.Locations[0].EndsAt = @event.StartTime;

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
        return @event.Id;
    }

    private static async Task<Guid> SeedPublishReadyDraftForHostAsync(
        string databaseName,
        FakeUserIdentityAccessor identity,
        Guid hostParticipantId,
        string title)
    {
        await using var context = CreateContext(databaseName);
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var user = (await currentUserService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var createResult = EventService.Create(EventTier.Small, title, hostParticipantId);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;

        var categoryId = SeedCategoryInContext(context);
        MakePublishReadyViaUpdate(@event, categoryId, user.Id);

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
        return @event.Id;
    }

    private static async Task SeedRecentPublishedEventsAsync(
        string databaseName,
        FakeUserIdentityAccessor identity,
        int count,
        bool cancelled = false)
    {
        await using var context = CreateContext(databaseName);
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var user = (await currentUserService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var categoryId = SeedCategoryInContext(context);

        for (var i = 0; i < count; i++)
        {
            var createResult = EventService.Create(EventTier.Small, $"Prior Event {i}", user.Id);
            var @event = createResult.Value.Event;
            var organizer = createResult.Value.Organizer;

            MakePublishReadyViaUpdate(
                @event,
                categoryId,
                user.Id,
                address: $"{100 + i} Main St");
            EventService.Publish(@event, categoryExists: true, recentPublishCount: 0, maxPublishesPerWeek: 100);

            if (cancelled)
            {
                EventService.Cancel(@event);
            }

            context.Events.Add(@event);
            context.EventOrganizers.Add(organizer);
        }

        await context.SaveChangesAsync(CancellationToken.None);
    }

    private static async Task<Guid> SeedDraftEventAsync(
        string databaseName,
        FakeUserIdentityAccessor identity,
        string title)
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

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
        return @event.Id;
    }

    private static async Task SeedPublishedVirtualOnlyEventAsync(
        string databaseName,
        FakeUserIdentityAccessor identity)
    {
        await using var context = CreateContext(databaseName);
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var user = (await currentUserService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var createResult = EventService.Create(EventTier.Small, "Virtual Only", user.Id);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;

        var categoryId = SeedCategoryInContext(context);
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
                    Name = "Stream",
                    Kind = EventLocationKind.Virtual,
                    Url = "https://stream.example.com/live"
                }
            ]
        };

        var updateResult = EventService.Update(@event, patch, user.Id);
        if (updateResult.IsFailure)
        {
            throw new InvalidOperationException(updateResult.Error.Code);
        }

        EventService.Publish(@event, categoryExists: true, recentPublishCount: 0, maxPublishesPerWeek: MaxPublishesPerWeek);

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    private static async Task<Guid> SeedPublishedEventAsync(
        string databaseName,
        FakeUserIdentityAccessor identity)
    {
        await using var context = CreateContext(databaseName);
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var user = (await currentUserService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var createResult = EventService.Create(EventTier.Small, "Already Published", user.Id);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;

        var categoryId = SeedCategoryInContext(context);
        MakePublishReadyViaUpdate(@event, categoryId, user.Id);
        EventService.Publish(@event, categoryExists: true, recentPublishCount: 0, maxPublishesPerWeek: MaxPublishesPerWeek);

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
        return @event.Id;
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

    private static void MakePublishReadyViaUpdate(
        Event @event,
        Guid categoryId,
        Guid actingUserId,
        AdmissionType admissionType = AdmissionType.Free,
        string address = "123 Main St",
        string city = "Sofia")
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
            AdmissionType = admissionType,
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

    private static PublishEventCommandHandler CreateHandler(
        ApplicationDbContext context,
        IUserIdentityAccessor identity)
    {
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));

        return new PublishEventCommandHandler(
            context,
            currentUserService,
            identity,
            new EventAccessService(context),
            new EventVenueConflictService(context),
            Options.Create(new EventOptions { MaxPublishesPerHostPerWeek = MaxPublishesPerWeek }));
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
