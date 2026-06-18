using Application.Events.Services;
using Domain.Events;
using Domain.Events.EventLocations;
using Domain.Events.Services;
using Domain.Groups;
using Domain.Groups.Services;
using Domain.Users;
using Domain.Users.Services;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace ApplicationTests.Events.Services;

public class EventAccessServiceResolveAttendeeTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";
    private const string DefaultGroupImageUrl = "/images/default-group.png";

    [Fact]
    public async Task ResolveAttendeeParticipantAsync_Should_ReturnSelf_When_ParticipantIdIsNull()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var user = CreateUser("auth0|rsvp-self-null", "self-null@example.com");
        await SeedUserAsync(databaseName, user);
        var service = CreateService(databaseName);

        // Act
        var result = await service.ResolveAttendeeParticipantAsync(null, user.Id, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(user.Id, result.Value.ParticipantId);
        Assert.False(result.Value.ParticipantIsGroup);
    }

    [Fact]
    public async Task ResolveAttendeeParticipantAsync_Should_ReturnSelf_When_ParticipantIdIsCallerId()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var user = CreateUser("auth0|rsvp-self-explicit", "self-explicit@example.com");
        await SeedUserAsync(databaseName, user);
        var service = CreateService(databaseName);

        // Act
        var result = await service.ResolveAttendeeParticipantAsync(user.Id, user.Id, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(user.Id, result.Value.ParticipantId);
        Assert.False(result.Value.ParticipantIsGroup);
    }

    [Theory]
    [InlineData(GroupMemberRole.Organizer)]
    [InlineData(GroupMemberRole.Administrator)]
    public async Task ResolveAttendeeParticipantAsync_Should_ReturnGroup_When_CallerHasOrganizerPlusRole(
        GroupMemberRole role)
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|rsvp-group-owner", "group-owner@example.com");
        var actor = CreateUser($"auth0|rsvp-group-{role}", $"{role}@example.com");
        var group = SeedGroupWithMember(databaseName, owner, actor, role);
        var service = CreateService(databaseName);

        // Act
        var result = await service.ResolveAttendeeParticipantAsync(group.Id, actor.Id, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(group.Id, result.Value.ParticipantId);
        Assert.True(result.Value.ParticipantIsGroup);
    }

    [Fact]
    public async Task ResolveAttendeeParticipantAsync_Should_ReturnGroup_When_CallerIsGroupOwner()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|rsvp-group-owner-role", "owner-role@example.com");
        var group = SeedGroupWithMember(databaseName, owner, owner, GroupMemberRole.Owner);
        var service = CreateService(databaseName);

        // Act
        var result = await service.ResolveAttendeeParticipantAsync(group.Id, owner.Id, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(group.Id, result.Value.ParticipantId);
        Assert.True(result.Value.ParticipantIsGroup);
    }

    [Fact]
    public async Task ResolveAttendeeParticipantAsync_Should_ReturnInsufficientHostPermissions_When_CallerIsGroupMember()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|rsvp-member-owner", "member-owner@example.com");
        var member = CreateUser("auth0|rsvp-member", "member@example.com");
        var group = SeedGroupWithMember(databaseName, owner, member, GroupMemberRole.Member);
        var service = CreateService(databaseName);

        // Act
        var result = await service.ResolveAttendeeParticipantAsync(group.Id, member.Id, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.InsufficientHostPermissions", result.Error.Code);
    }

    [Fact]
    public async Task ResolveAttendeeParticipantAsync_Should_ReturnHostNotFound_When_GroupIsSoftDeleted()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|rsvp-deleted-owner", "deleted-owner@example.com");
        var group = SeedGroupWithMember(databaseName, owner, owner, GroupMemberRole.Owner);

        await using (var seedContext = CreateContext(databaseName))
        {
            var persistedGroup = await seedContext.Groups.SingleAsync();
            GroupService.SoftDelete(persistedGroup);
            await seedContext.SaveChangesAsync();
        }

        var service = CreateService(databaseName);

        // Act
        var result = await service.ResolveAttendeeParticipantAsync(group.Id, owner.Id, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.HostNotFound", result.Error.Code);
    }

    [Fact]
    public async Task ResolveAttendeeParticipantAsync_Should_ReturnHostNotFound_When_ParticipantIdIsUnknown()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var user = CreateUser("auth0|rsvp-unknown", "unknown@example.com");
        await SeedUserAsync(databaseName, user);
        var service = CreateService(databaseName);
        var unknownId = Guid.Parse("99999999-9999-9999-9999-999999999999");

        // Act
        var result = await service.ResolveAttendeeParticipantAsync(unknownId, user.Id, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.HostNotFound", result.Error.Code);
    }

    [Fact]
    public async Task ResolveAttendeeParticipantAsync_Should_ReturnInsufficientHostPermissions_When_ParticipantIdIsAnotherUser()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var actor = CreateUser("auth0|rsvp-actor", "actor@example.com");
        var otherUser = CreateUser("auth0|rsvp-other", "other@example.com");
        await using (var seedContext = CreateContext(databaseName))
        {
            seedContext.Users.AddRange(actor, otherUser);
            await seedContext.SaveChangesAsync();
        }

        var service = CreateService(databaseName);

        // Act
        var result = await service.ResolveAttendeeParticipantAsync(otherUser.Id, actor.Id, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.InsufficientHostPermissions", result.Error.Code);
    }

    [Fact]
    public async Task GetActiveEventWithAttendeesAsync_Should_IncludeAttendees_When_EventExists()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var user = CreateUser("auth0|rsvp-load", "load@example.com");
        var (eventId, attendeeId) = await SeedPublishedEventWithAttendeeAsync(databaseName, user);
        var service = CreateService(databaseName);

        // Act
        var result = await service.GetActiveEventWithAttendeesAsync(eventId, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Organizers);
        Assert.Single(result.Value.Attendees);
        Assert.Equal(attendeeId, result.Value.Attendees[0].ParticipantId);
    }

    private static async Task<(Guid EventId, Guid AttendeeId)> SeedPublishedEventWithAttendeeAsync(string databaseName, User user)
    {
        var createResult = EventService.Create(EventTier.Small, "RSVP Event", user.Id);
        var @event = createResult.Value.Event;
        @event.Description = "Description";
        @event.CategoryId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        @event.StartTime = new DateTime(2026, 8, 1, 18, 0, 0, DateTimeKind.Utc);
        @event.EndTime = new DateTime(2026, 8, 1, 22, 0, 0, DateTimeKind.Utc);
        @event.TimeZoneId = "Europe/Sofia";
        @event.AdmissionType = AdmissionType.Free;
        @event.Locations.Add(new EventLocation
        {
            Name = "Hall",
            Kind = EventLocationKind.Physical,
            Address = "123 Main St",
            City = "Sofia"
        });

        EventService.Publish(@event, categoryExists: true, recentPublishCount: 0, maxPublishesPerWeek: 6);
        var attendeeUser = CreateUser("auth0|rsvp-load-attendee", "load-attendee@example.com");
        EventAttendeeService.SetRsvpStatus(
            @event,
            attendeeUser.Id,
            user.Id,
            EventAttendeeStatus.Interested,
            DateTime.UtcNow);

        await using var seedContext = CreateContext(databaseName);
        seedContext.Users.AddRange(user, attendeeUser);
        seedContext.Events.Add(@event);
        seedContext.EventOrganizers.Add(createResult.Value.Organizer);
        await seedContext.SaveChangesAsync();

        return (@event.Id, attendeeUser.Id);
    }

    private static async Task SeedUserAsync(string databaseName, User user)
    {
        await using var seedContext = CreateContext(databaseName);
        seedContext.Users.Add(user);
        await seedContext.SaveChangesAsync();
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

    private static User CreateUser(string subject, string email) =>
        UserService.ProvisionFromExternalIdentity(
            subject,
            email,
            emailVerified: true,
            profilePictureUrl: null,
            DefaultAvatarUrl,
            ServiceRole.User);

    private static EventAccessService CreateService(string databaseName) =>
        new(CreateContext(databaseName));

    private static ApplicationDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new ApplicationDbContext(options);
    }
}
