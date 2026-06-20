using Application.Groups.ProcessGroupSoftDeleted;
using Domain.Events;
using Domain.Events.EventLocations;
using Domain.Events.Events;
using Domain.Events.Services;
using Domain.Groups;
using Domain.Groups.Events;
using Domain.Groups.Services;
using Domain.Users;
using Domain.Users.Services;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApplicationTests.Groups.ProcessGroupSoftDeleted;

public class GroupSoftDeletedCascadeHandlerTests
{
    private const string DefaultGroupImageUrl = "/images/default-group.png";
    private static readonly DateTime FixedUtcNow = new(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task HandleAsync_Should_CancelFuturePublishedGroupHostedEvent_When_EventStartsAfterUtcNow()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|cascade-owner", "owner@example.com");
        var (groupId, eventId) = await SeedSoftDeletedGroupWithEventAsync(
            databaseName,
            owner,
            FixedUtcNow.AddDays(7),
            EventStatus.Published);
        var handler = CreateHandler(databaseName, FixedUtcNow);

        // Act
        await handler.HandleAsync(new GroupSoftDeleted(groupId), CancellationToken.None);

        // Assert
        await using var context = CreateContext(databaseName);
        var @event = await context.Events.SingleAsync(e => e.Id == eventId);
        Assert.Equal(EventStatus.Cancelled, @event.Status);
    }

    [Fact]
    public async Task HandleAsync_Should_LeavePastPublishedGroupHostedEvent_When_EventStartsBeforeUtcNow()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|cascade-past", "past@example.com");
        var (groupId, eventId) = await SeedSoftDeletedGroupWithEventAsync(
            databaseName,
            owner,
            FixedUtcNow.AddDays(-1),
            EventStatus.Published);
        var handler = CreateHandler(databaseName, FixedUtcNow);

        // Act
        await handler.HandleAsync(new GroupSoftDeleted(groupId), CancellationToken.None);

        // Assert
        await using var context = CreateContext(databaseName);
        var @event = await context.Events.SingleAsync(e => e.Id == eventId);
        Assert.Equal(EventStatus.Published, @event.Status);
    }

    [Fact]
    public async Task HandleAsync_Should_LeaveDraftGroupHostedEvent_When_EventIsDraft()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|cascade-draft", "draft@example.com");
        var (groupId, eventId) = await SeedSoftDeletedGroupWithEventAsync(
            databaseName,
            owner,
            FixedUtcNow.AddDays(7),
            EventStatus.Draft);
        var handler = CreateHandler(databaseName, FixedUtcNow);

        // Act
        await handler.HandleAsync(new GroupSoftDeleted(groupId), CancellationToken.None);

        // Assert
        await using var context = CreateContext(databaseName);
        var @event = await context.Events.SingleAsync(e => e.Id == eventId);
        Assert.Equal(EventStatus.Draft, @event.Status);
    }

    [Fact]
    public async Task HandleAsync_Should_SkipAlreadyCancelledFutureEvent_When_EventIsCancelled()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|cascade-cancelled", "cancelled@example.com");
        var (groupId, eventId) = await SeedSoftDeletedGroupWithEventAsync(
            databaseName,
            owner,
            FixedUtcNow.AddDays(7),
            EventStatus.Cancelled);
        var handler = CreateHandler(databaseName, FixedUtcNow);

        // Act
        await handler.HandleAsync(new GroupSoftDeleted(groupId), CancellationToken.None);

        // Assert
        await using var context = CreateContext(databaseName);
        var @event = await context.Events.SingleAsync(e => e.Id == eventId);
        Assert.Equal(EventStatus.Cancelled, @event.Status);
    }

    [Fact]
    public async Task HandleAsync_Should_LeaveUserHostedFutureEvent_When_OrganizerIsNotGroup()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|cascade-user-host", "user-host@example.com");
        var groupCreateResult = GroupService.Create(
            "Cascade Org",
            "",
            GroupJoinPolicy.Open,
            owner.Id,
            DefaultGroupImageUrl);
        var group = groupCreateResult.Value.Group;
        GroupService.SoftDelete(group);

        var userEventCreate = EventService.Create(EventTier.Small, "User Hosted", owner.Id);
        var userEvent = userEventCreate.Value.Event;
        MakePublishReady(userEvent, owner.Id, FixedUtcNow.AddDays(7));
        EventService.Publish(userEvent, categoryExists: true, recentPublishCount: 0, maxPublishesPerWeek: 6);

        await using (var seedContext = CreateContext(databaseName))
        {
            seedContext.Users.Add(owner);
            seedContext.Groups.Add(group);
            seedContext.GroupMemberships.Add(groupCreateResult.Value.OwnerMembership);
            seedContext.Events.Add(userEvent);
            seedContext.EventOrganizers.Add(userEventCreate.Value.Organizer);
            await seedContext.SaveChangesAsync();
        }

        var handler = CreateHandler(databaseName, FixedUtcNow);

        // Act
        await handler.HandleAsync(new GroupSoftDeleted(group.Id), CancellationToken.None);

        // Assert
        await using var context = CreateContext(databaseName);
        var @event = await context.Events.SingleAsync(e => e.Id == userEvent.Id);
        Assert.Equal(EventStatus.Published, @event.Status);
    }

    [Fact]
    public async Task HandleAsync_Should_NotThrow_When_NoMatchingEventsExist()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|cascade-empty", "empty@example.com");
        var createResult = GroupService.Create(
            "Empty Org",
            "",
            GroupJoinPolicy.Open,
            owner.Id,
            DefaultGroupImageUrl);
        var group = createResult.Value.Group;
        GroupService.SoftDelete(group);

        await using (var seedContext = CreateContext(databaseName))
        {
            seedContext.Users.Add(owner);
            seedContext.Groups.Add(group);
            seedContext.GroupMemberships.Add(createResult.Value.OwnerMembership);
            await seedContext.SaveChangesAsync();
        }

        var handler = CreateHandler(databaseName, FixedUtcNow);

        // Act
        var exception = await Record.ExceptionAsync(() =>
            handler.HandleAsync(new GroupSoftDeleted(group.Id), CancellationToken.None));

        // Assert
        Assert.Null(exception);
    }

    private static GroupSoftDeletedCascadeHandler CreateHandler(
        string databaseName,
        DateTime utcNow)
    {
        var context = CreateContext(databaseName);
        return new GroupSoftDeletedCascadeHandler(
            context,
            new FixedTimeProvider(utcNow),
            NullLogger<GroupSoftDeletedCascadeHandler>.Instance);
    }

    private static async Task<(Guid GroupId, Guid EventId)> SeedSoftDeletedGroupWithEventAsync(
        string databaseName,
        User owner,
        DateTime startTime,
        EventStatus statusAfterSeed)
    {
        var groupCreateResult = GroupService.Create(
            "Hosted Org",
            "",
            GroupJoinPolicy.Open,
            owner.Id,
            DefaultGroupImageUrl);
        var group = groupCreateResult.Value.Group;

        var eventCreateResult = EventService.Create(EventTier.Small, "Group Event", group.Id);
        var @event = eventCreateResult.Value.Event;
        MakePublishReady(@event, owner.Id, startTime);

        if (statusAfterSeed == EventStatus.Published)
        {
            EventService.Publish(@event, categoryExists: true, recentPublishCount: 0, maxPublishesPerWeek: 6);
        }
        else if (statusAfterSeed == EventStatus.Cancelled)
        {
            EventService.Publish(@event, categoryExists: true, recentPublishCount: 0, maxPublishesPerWeek: 6);
            EventService.Cancel(@event);
            @event.ClearDomainEvents();
        }

        GroupService.SoftDelete(group);
        group.ClearDomainEvents();
        @event.ClearDomainEvents();

        await using var context = CreateContext(databaseName);
        context.Users.Add(owner);
        context.Groups.Add(group);
        context.GroupMemberships.Add(groupCreateResult.Value.OwnerMembership);
        context.Events.Add(@event);
        context.EventOrganizers.Add(eventCreateResult.Value.Organizer);
        await context.SaveChangesAsync();

        return (group.Id, @event.Id);
    }

    private static void MakePublishReady(Event @event, Guid actingUserId, DateTime startTime)
    {
        var endTime = startTime.AddHours(4);
        var patch = new EventUpdatePatch
        {
            Description = "Group-hosted event",
            CategoryId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            StartTime = startTime,
            EndTime = endTime,
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

    private static ApplicationDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new ApplicationDbContext(options);
    }

    private static User CreateUser(string subject, string email) =>
        UserService.ProvisionFromExternalIdentity(
            subject,
            email,
            emailVerified: true,
            profilePictureUrl: null,
            "/images/default-avatar.png",
            ServiceRole.User);

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(utcNow, TimeSpan.Zero);
    }
}
