using Application.Abstractions.Authentication;
using Application.Events.SetEventRsvp;
using Application.Events.Services;
using Application.Users;
using Application.Users.Services;
using Domain.Events;
using Domain.Events.EventLocations;
using Domain.Events.Services;
using Domain.Groups;
using Domain.Groups.Services;
using Domain.Tickets.Services;
using Domain.Users;
using Domain.Users.Services;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApplicationTests.Events.SetEventRsvp;

public class SetEventRsvpCommandHandlerTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";
    private const string DefaultGroupImageUrl = "/images/default-group.png";

    [Fact]
    public async Task SetEventRsvpCommandHandler_Should_SetGoingWithRegisteredAt_When_SelfRsvpOnPublishedFreeEvent()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|rsvp-going", "going@example.com");
        var eventId = await SeedPublishedFreeEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);
        var user = await ProvisionUserAsync(context, identity);

        // Act
        var result = await handler.Handle(
            new SetEventRsvpCommand(eventId, ParticipantId: null, EventAttendeeStatus.Going),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(eventId, result.Value.EventId);
        Assert.Equal(user.Id, result.Value.ParticipantId);
        Assert.False(result.Value.ParticipantIsGroup);
        Assert.Equal(EventAttendeeStatus.Going, result.Value.Status);
        Assert.NotNull(result.Value.RegisteredAt);

        await using var verifyContext = CreateContext(databaseName);
        var attendee = await verifyContext.EventAttendees.SingleAsync();
        Assert.Equal(user.Id, attendee.ParticipantId);
        Assert.Equal(EventAttendeeStatus.Going, attendee.Status);
        Assert.NotNull(attendee.RegisteredAt);
    }

    [Fact]
    public async Task SetEventRsvpCommandHandler_Should_SetInterestedAsGroup_When_CallerIsOrganizer()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|rsvp-org-owner", "org-owner@example.com");
        var organizer = CreateUser("auth0|rsvp-org-organizer", "org-organizer@example.com");
        var group = SeedGroupWithMember(databaseName, owner, organizer, GroupMemberRole.Organizer);
        var identity = CreateVerifiedIdentity("auth0|rsvp-org-organizer", "org-organizer@example.com");
        var eventId = await SeedPublishedFreeEventAsync(databaseName, CreateVerifiedIdentity("auth0|host", "host@example.com"));
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(
            new SetEventRsvpCommand(eventId, group.Id, EventAttendeeStatus.Interested),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(group.Id, result.Value.ParticipantId);
        Assert.True(result.Value.ParticipantIsGroup);
        Assert.Equal(EventAttendeeStatus.Interested, result.Value.Status);
    }

    [Fact]
    public async Task SetEventRsvpCommandHandler_Should_PersistAttendeeRow_When_ReloadingFromDatabase()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|rsvp-persist", "persist@example.com");
        var eventId = await SeedPublishedFreeEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);
        var user = await ProvisionUserAsync(context, identity);

        // Act
        var result = await handler.Handle(
            new SetEventRsvpCommand(eventId, user.Id, EventAttendeeStatus.Going),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        await using var verifyContext = CreateContext(databaseName);
        var attendee = await verifyContext.EventAttendees
            .AsNoTracking()
            .SingleAsync(a => a.EventId == eventId && a.ParticipantId == user.Id);
        Assert.Equal(EventAttendeeStatus.Going, attendee.Status);
    }

    [Fact]
    public async Task SetEventRsvpCommandHandler_Should_ReturnEmailNotVerified_When_CallerIsUnverified()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|rsvp-unverified", "unverified@example.com");
        identity.EmailVerified = false;
        var eventId = await SeedPublishedFreeEventAsync(databaseName, CreateVerifiedIdentity("auth0|host2", "host2@example.com"));
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(
            new SetEventRsvpCommand(eventId, null, EventAttendeeStatus.Going),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Users.EmailNotVerified", result.Error.Code);
    }

    [Fact]
    public async Task SetEventRsvpCommandHandler_Should_ReturnPaidAdmissionNotAllowed_When_EventIsPaid()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|rsvp-paid", "paid@example.com");
        var eventId = await SeedPublishedFreeEventAsync(databaseName, identity, AdmissionType.Paid);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(
            new SetEventRsvpCommand(eventId, null, EventAttendeeStatus.Going),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("EventAttendees.PaidAdmissionNotAllowed", result.Error.Code);
    }

    [Fact]
    public async Task SetEventRsvpCommandHandler_Should_ReturnCannotModifyCancelled_When_EventIsCancelled()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|rsvp-cancelled", "cancelled@example.com");
        var eventId = await SeedPublishedFreeEventAsync(databaseName, identity, cancel: true);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(
            new SetEventRsvpCommand(eventId, null, EventAttendeeStatus.Going),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.CannotModifyCancelled", result.Error.Code);
    }

    [Fact]
    public async Task SetEventRsvpCommandHandler_Should_ReturnNotPublished_When_EventIsDraft()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|rsvp-draft", "draft@example.com");
        var eventId = await SeedDraftEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(
            new SetEventRsvpCommand(eventId, null, EventAttendeeStatus.Going),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.NotPublished", result.Error.Code);
    }

    [Fact]
    public async Task SetEventRsvpCommandHandler_Should_ReturnInsufficientHostPermissions_When_CallerIsGroupMember()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|rsvp-member-owner", "member-owner@example.com");
        var member = CreateUser("auth0|rsvp-member-only", "member-only@example.com");
        var group = SeedGroupWithMember(databaseName, owner, member, GroupMemberRole.Member);
        var identity = CreateVerifiedIdentity("auth0|rsvp-member-only", "member-only@example.com");
        var eventId = await SeedPublishedFreeEventAsync(databaseName, CreateVerifiedIdentity("auth0|host3", "host3@example.com"));
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(
            new SetEventRsvpCommand(eventId, group.Id, EventAttendeeStatus.Going),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.InsufficientHostPermissions", result.Error.Code);
    }

    [Fact]
    public async Task SetEventRsvpCommandHandler_Should_ReturnHostMustRemainGoing_When_HostSetsInterested()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|rsvp-host", "host@example.com");
        var eventId = await SeedPublishedFreeEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);
        var user = await ProvisionUserAsync(context, identity);

        // Act
        var result = await handler.Handle(
            new SetEventRsvpCommand(eventId, user.Id, EventAttendeeStatus.Interested),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("EventAttendees.HostMustRemainGoing", result.Error.Code);
    }

    [Fact]
    public async Task SetEventRsvpCommandHandler_Should_Succeed_When_StatusIsUnchanged()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|rsvp-idempotent", "idempotent@example.com");
        var eventId = await SeedPublishedFreeEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);
        var command = new SetEventRsvpCommand(eventId, null, EventAttendeeStatus.Going);

        // Act
        var first = await handler.Handle(command, CancellationToken.None);
        var second = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);

        await using var verifyContext = CreateContext(databaseName);
        Assert.Single(await verifyContext.EventAttendees.ToListAsync());
    }

    private static async Task<Guid> SeedPublishedFreeEventAsync(
        string databaseName,
        FakeUserIdentityAccessor identity,
        AdmissionType admissionType = AdmissionType.Free,
        bool cancel = false)
    {
        await using var context = CreateContext(databaseName);
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var user = (await currentUserService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var createResult = EventService.Create(EventTier.Small, "RSVP Test Event", user.Id);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;

        var categoryId = SeedCategoryInContext(context);
        MakePublishReady(@event, categoryId, user.Id, admissionType);

        if (admissionType == AdmissionType.Paid)
        {
            TicketTypeService.Create(@event, "General Admission", "Standard entry", 2500, 100);
        }

        EventService.Publish(@event, categoryExists: true, recentPublishCount: 0, maxPublishesPerWeek: 6);

        if (cancel)
        {
            EventService.Cancel(@event);
        }

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
        return @event.Id;
    }

    private static async Task<Guid> SeedDraftEventAsync(
        string databaseName,
        FakeUserIdentityAccessor identity)
    {
        await using var context = CreateContext(databaseName);
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var user = (await currentUserService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var createResult = EventService.Create(EventTier.Small, "Draft RSVP Event", user.Id);
        context.Events.Add(createResult.Value.Event);
        context.EventOrganizers.Add(createResult.Value.Organizer);
        await context.SaveChangesAsync(CancellationToken.None);
        return createResult.Value.Event.Id;
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

    private static void MakePublishReady(
        Event @event,
        Guid categoryId,
        Guid actingUserId,
        AdmissionType admissionType)
    {
        var patch = new EventUpdatePatch
        {
            Description = "A test event description",
            CategoryId = categoryId,
            StartTime = new DateTime(2026, 7, 1, 18, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 7, 1, 22, 0, 0, DateTimeKind.Utc),
            TimeZoneId = "Europe/Sofia",
            AdmissionType = admissionType,
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
            throw new InvalidOperationException(updateResult.Error.Message);
        }
    }

    private static Group SeedGroupWithMember(
        string databaseName,
        User owner,
        User member,
        GroupMemberRole memberRole)
    {
        var createResult = GroupService.Create(
            "RSVP Org",
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

    private static SetEventRsvpCommandHandler CreateHandler(
        ApplicationDbContext context,
        IUserIdentityAccessor identity)
    {
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));

        return new SetEventRsvpCommandHandler(
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
        public bool IsAuthenticated { get; set; }
        public string? ExternalSubjectId { get; set; }
        public string? Email { get; set; }
        public bool EmailVerified { get; set; }
        public ServiceRole ServiceRole { get; set; }
        public string? ProfilePictureUrl { get; set; }
    }
}
