using Application.Abstractions.Authentication;
using Application.Events.ListMyRsvps;
using Application.Events.Services;
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

namespace ApplicationTests.Events.ListMyRsvps;

public class ListMyRsvpsQueryHandlerTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";
    private const string DefaultGroupImageUrl = "/images/default-group.png";

    [Fact]
    public async Task ListMyRsvpsQueryHandler_Should_ReturnSelfGoingRow_When_UserRsvpedOnPublishedFreeEvent()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|my-rsvp-going", "my-rsvp-going@example.com");
        await SeedPublishedEventWithAttendeeAsync(
            databaseName,
            hostIdentity: CreateVerifiedIdentity("auth0|host-going", "host-going@example.com"),
            attendeeIdentity: identity,
            attendeeStatus: EventAttendeeStatus.Going);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new ListMyRsvpsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        var item = result.Value[0];
        Assert.Equal(EventAttendeeStatus.Going, item.RsvpStatus);
        Assert.False(item.ParticipantIsGroup);
        Assert.Equal("my-rsvp-going", item.ParticipantDisplayName);
    }

    [Fact]
    public async Task ListMyRsvpsQueryHandler_Should_ReturnGroupInterestedRow_When_OrganizerPlusRsvpedAsGroup()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|list-rsvp-owner", "list-rsvp-owner@example.com");
        var organizer = CreateUser("auth0|list-rsvp-organizer", "list-rsvp-organizer@example.com");
        var group = SeedGroupWithMember(databaseName, owner, organizer, GroupMemberRole.Organizer, "RSVP Org");
        var organizerIdentity = CreateVerifiedIdentity("auth0|list-rsvp-organizer", "list-rsvp-organizer@example.com");
        await SeedPublishedEventWithAttendeeAsync(
            databaseName,
            hostIdentity: CreateVerifiedIdentity("auth0|list-rsvp-host", "list-rsvp-host@example.com"),
            attendeeParticipantId: group.Id,
            attendeeStatus: EventAttendeeStatus.Interested);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, organizerIdentity);

        // Act
        var result = await handler.Handle(new ListMyRsvpsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        var item = result.Value[0];
        Assert.True(item.ParticipantIsGroup);
        Assert.Equal(group.Id, item.ParticipantId);
        Assert.Equal("RSVP Org", item.ParticipantDisplayName);
        Assert.Equal(EventAttendeeStatus.Interested, item.RsvpStatus);
    }

    [Fact]
    public async Task ListMyRsvpsQueryHandler_Should_ExcludeNotGoingRows_When_AttendeeDeclined()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|my-rsvp-notgoing", "my-rsvp-notgoing@example.com");
        await SeedPublishedEventWithAttendeeAsync(
            databaseName,
            hostIdentity: CreateVerifiedIdentity("auth0|host-notgoing", "host-notgoing@example.com"),
            attendeeIdentity: identity,
            attendeeStatus: EventAttendeeStatus.NotGoing);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new ListMyRsvpsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task ListMyRsvpsQueryHandler_Should_IncludeCancelledFreeEvent_When_UserHasGoingRow()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|my-rsvp-cancel", "my-rsvp-cancel@example.com");
        await SeedPublishedEventWithAttendeeAsync(
            databaseName,
            hostIdentity: CreateVerifiedIdentity("auth0|host-cancel", "host-cancel@example.com"),
            attendeeIdentity: identity,
            attendeeStatus: EventAttendeeStatus.Going,
            cancelled: true);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new ListMyRsvpsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal(EventStatus.Cancelled, result.Value[0].Status);
    }

    [Fact]
    public async Task ListMyRsvpsQueryHandler_Should_ExcludeDraftAndSoftDeletedEvents_When_AttendeeRowsExist()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|my-rsvp-hidden", "my-rsvp-hidden@example.com");
        await SeedDraftEventWithAttendeeAsync(databaseName, identity);
        await SeedPublishedEventWithAttendeeAsync(
            databaseName,
            hostIdentity: CreateVerifiedIdentity("auth0|host-deleted", "host-deleted@example.com"),
            attendeeIdentity: identity,
            attendeeStatus: EventAttendeeStatus.Going,
            softDeleted: true);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new ListMyRsvpsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task ListMyRsvpsQueryHandler_Should_ReturnTwoRows_When_SelfAndGroupRsvpOnSameEvent()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|dual-rsvp-owner", "dual-rsvp-owner@example.com");
        var organizer = CreateUser("auth0|dual-rsvp-organizer", "dual-rsvp-organizer@example.com");
        var group = SeedGroupWithMember(databaseName, owner, organizer, GroupMemberRole.Organizer);
        var organizerIdentity = CreateVerifiedIdentity("auth0|dual-rsvp-organizer", "dual-rsvp-organizer@example.com");
        var eventId = await SeedPublishedEventWithAttendeesAsync(
            databaseName,
            hostIdentity: CreateVerifiedIdentity("auth0|dual-rsvp-host", "dual-rsvp-host@example.com"),
            attendees:
            [
                (organizer.Id, EventAttendeeStatus.Going),
                (group.Id, EventAttendeeStatus.Interested)
            ]);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, organizerIdentity);

        // Act
        var result = await handler.Handle(new ListMyRsvpsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.All(result.Value, item => Assert.Equal(eventId, item.EventId));
        Assert.Contains(result.Value, item => !item.ParticipantIsGroup && item.RsvpStatus == EventAttendeeStatus.Going);
        Assert.Contains(result.Value, item => item.ParticipantIsGroup && item.RsvpStatus == EventAttendeeStatus.Interested);
    }

    [Fact]
    public async Task ListMyRsvpsQueryHandler_Should_ReturnEmailNotVerified_When_CallerIsUnverified()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = new FakeUserIdentityAccessor
        {
            IsAuthenticated = true,
            ExternalSubjectId = "auth0|unverified-rsvp",
            Email = "unverified-rsvp@example.com",
            EmailVerified = false
        };
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new ListMyRsvpsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Users.EmailNotVerified", result.Error.Code);
    }

    [Fact]
    public async Task ListMyRsvpsQueryHandler_Should_ReturnEmptyList_When_NoMatchingRows()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|my-rsvp-empty", "my-rsvp-empty@example.com");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new ListMyRsvpsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task ListMyRsvpsQueryHandler_Should_SortByStartTimeAscending_When_MultipleEvents()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|my-rsvp-sort", "my-rsvp-sort@example.com");
        var laterStart = new DateTime(2026, 9, 1, 18, 0, 0, DateTimeKind.Utc);
        var earlierStart = new DateTime(2026, 8, 1, 18, 0, 0, DateTimeKind.Utc);
        await SeedPublishedEventWithAttendeeAsync(
            databaseName,
            hostIdentity: CreateVerifiedIdentity("auth0|host-later", "host-later@example.com"),
            attendeeIdentity: identity,
            attendeeStatus: EventAttendeeStatus.Going,
            title: "Later Event",
            startTime: laterStart);
        await SeedPublishedEventWithAttendeeAsync(
            databaseName,
            hostIdentity: CreateVerifiedIdentity("auth0|host-earlier", "host-earlier@example.com"),
            attendeeIdentity: identity,
            attendeeStatus: EventAttendeeStatus.Interested,
            title: "Earlier Event",
            startTime: earlierStart);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new ListMyRsvpsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal("Earlier Event", result.Value[0].Title);
        Assert.Equal("Later Event", result.Value[1].Title);
    }

    private static async Task SeedPublishedEventWithAttendeeAsync(
        string databaseName,
        FakeUserIdentityAccessor hostIdentity,
        FakeUserIdentityAccessor? attendeeIdentity = null,
        Guid? attendeeParticipantId = null,
        EventAttendeeStatus attendeeStatus = EventAttendeeStatus.Going,
        bool cancelled = false,
        bool softDeleted = false,
        string title = "RSVP List Event",
        DateTime? startTime = null)
    {
        await using var context = CreateContext(databaseName);
        var hostUser = await ProvisionUserAsync(context, hostIdentity);
        Guid participantId;
        if (attendeeParticipantId is not null)
        {
            participantId = attendeeParticipantId.Value;
        }
        else
        {
            var attendeeUser = await ProvisionUserAsync(context, attendeeIdentity!);
            participantId = attendeeUser.Id;
            if (!string.IsNullOrWhiteSpace(attendeeIdentity!.Email.Split('@')[0]))
            {
                attendeeUser.Username = attendeeIdentity.Email.Split('@')[0];
            }
        }

        var createResult = EventService.Create(EventTier.Small, title, hostUser.Id);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;
        var categoryId = SeedCategoryInContext(context);
        var eventStart = startTime ?? new DateTime(2026, 7, 1, 18, 0, 0, DateTimeKind.Utc);
        MakePublishReady(@event, categoryId, hostUser.Id, eventStart);
        EventService.Publish(@event, categoryExists: true, recentPublishCount: 0, maxPublishesPerWeek: 100);

        if (cancelled)
        {
            EventService.Cancel(@event);
        }

        if (softDeleted)
        {
            @event.DeletedAt = DateTime.UtcNow;
        }

        @event.Attendees.Add(new EventAttendee
        {
            EventId = @event.Id,
            ParticipantId = participantId,
            Status = attendeeStatus,
            RegisteredAt = DateTime.UtcNow
        });

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    private static async Task<Guid> SeedPublishedEventWithAttendeesAsync(
        string databaseName,
        FakeUserIdentityAccessor hostIdentity,
        IReadOnlyList<(Guid ParticipantId, EventAttendeeStatus Status)> attendees)
    {
        await using var context = CreateContext(databaseName);
        var hostUser = await ProvisionUserAsync(context, hostIdentity);
        var createResult = EventService.Create(EventTier.Small, "Dual RSVP Event", hostUser.Id);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;
        var categoryId = SeedCategoryInContext(context);
        MakePublishReady(@event, categoryId, hostUser.Id, new DateTime(2026, 7, 1, 18, 0, 0, DateTimeKind.Utc));
        EventService.Publish(@event, categoryExists: true, recentPublishCount: 0, maxPublishesPerWeek: 100);

        var now = DateTime.UtcNow;
        foreach (var (participantId, status) in attendees)
        {
            @event.Attendees.Add(new EventAttendee
            {
                EventId = @event.Id,
                ParticipantId = participantId,
                Status = status,
                RegisteredAt = now
            });
        }

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
        return @event.Id;
    }

    private static async Task SeedDraftEventWithAttendeeAsync(
        string databaseName,
        FakeUserIdentityAccessor attendeeIdentity)
    {
        await using var context = CreateContext(databaseName);
        var attendeeUser = await ProvisionUserAsync(context, attendeeIdentity);
        var createResult = EventService.Create(EventTier.Small, "Draft RSVP Event", attendeeUser.Id);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;

        @event.Attendees.Add(new EventAttendee
        {
            EventId = @event.Id,
            ParticipantId = attendeeUser.Id,
            Status = EventAttendeeStatus.Going,
            RegisteredAt = DateTime.UtcNow
        });

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    private static void MakePublishReady(
        Event @event,
        Guid categoryId,
        Guid actingUserId,
        DateTime startTime)
    {
        var patch = new EventUpdatePatch
        {
            Description = "A test event description",
            CategoryId = categoryId,
            StartTime = startTime,
            EndTime = startTime.AddHours(4),
            TimeZoneId = "Europe/Sofia",
            AdmissionType = AdmissionType.Free,
            Locations =
            [
                new EventLocation
                {
                    Name = "Main Hall",
                    Kind = EventLocationKind.Physical,
                    Address = "1 Main St",
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

    private static Group SeedGroupWithMember(
        string databaseName,
        User owner,
        User member,
        GroupMemberRole memberRole,
        string groupName = "Host Org")
    {
        var createResult = GroupService.Create(
            groupName,
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

    private static ListMyRsvpsQueryHandler CreateHandler(
        ApplicationDbContext context,
        FakeUserIdentityAccessor identity)
    {
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));

        return new ListMyRsvpsQueryHandler(
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
