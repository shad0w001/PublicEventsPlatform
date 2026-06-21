using Domain.Events;
using Domain.Events.Services;
using Domain.Groups;
using Domain.Groups.Services;
using Domain.Users;
using Domain.Users.Services;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureTests;

public class ApplicationDbContextTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";
    private const string DefaultGroupImageUrl = "/images/default-group.png";

    [Fact]
    public void UserModel_Should_HaveUniqueIndexOnExternalSubjectId_When_ApplicationDbContextModelIsBuilt()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        var entityType = context.Model.FindEntityType(typeof(User));
        var index = entityType?.GetIndexes()
            .SingleOrDefault(i => i.Properties.Any(p => p.Name == nameof(User.ExternalSubjectId)));

        // Assert
        Assert.NotNull(entityType);
        Assert.NotNull(index);
        Assert.True(index.IsUnique);
    }

    [Fact]
    public void EventAttendeeModel_Should_NotContainShadowEventId1_When_ApplicationDbContextModelIsBuilt()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        var entityType = context.Model.FindEntityType(typeof(EventAttendee));
        var shadowProperty = entityType?.FindProperty("EventId1");

        // Assert
        Assert.NotNull(entityType);
        Assert.Null(shadowProperty);
    }

    [Fact]
    public void GroupMembershipModel_Should_NotContainShadowGroupId1_When_ApplicationDbContextModelIsBuilt()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        var entityType = context.Model.FindEntityType(typeof(GroupMembership));
        var shadowProperty = entityType?.FindProperty("GroupId1");

        // Assert
        Assert.NotNull(entityType);
        Assert.Null(shadowProperty);
    }

    [Fact]
    public void GroupJoinApplicationModel_Should_NotContainShadowGroupId1_When_ApplicationDbContextModelIsBuilt()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        var entityType = context.Model.FindEntityType(typeof(GroupJoinApplication));
        var shadowProperty = entityType?.FindProperty("GroupId1");

        // Assert
        Assert.NotNull(entityType);
        Assert.Null(shadowProperty);
    }

    [Fact]
    public void GroupJoinApplicationModel_Should_HaveFilteredUniqueIndex_When_ApplicationDbContextModelIsBuilt()
    {
        // Arrange
        using var context = CreateContext();

        // Act
        var entityType = context.Model.FindEntityType(typeof(GroupJoinApplication));
        var index = entityType?.GetIndexes()
            .SingleOrDefault(i =>
                i.Properties.Count == 2 &&
                i.Properties.Any(p => p.Name == nameof(GroupJoinApplication.GroupId)) &&
                i.Properties.Any(p => p.Name == nameof(GroupJoinApplication.UserId)));

        // Assert
        Assert.NotNull(entityType);
        Assert.NotNull(index);
        Assert.True(index.IsUnique);
        Assert.Equal("\"Status\" = 'Pending'", index.GetFilter());
    }

    [Fact]
    public async Task Group_Should_PersistJoinApplicationAndMembership_When_GroupAggregateIsSaved()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = UserService.ProvisionFromExternalIdentity(
            "auth0|owner-subject",
            "owner@example.com",
            emailVerified: true,
            profilePictureUrl: null,
            DefaultAvatarUrl,
            ServiceRole.User);
        var applicant = UserService.ProvisionFromExternalIdentity(
            "auth0|applicant-subject",
            "applicant@example.com",
            emailVerified: true,
            profilePictureUrl: null,
            DefaultAvatarUrl,
            ServiceRole.User);

        var createResult = GroupService.Create(
            "Test Org",
            "",
            GroupJoinPolicy.ApplicationRequired,
            owner.Id,
            DefaultGroupImageUrl);
        var group = createResult.Value.Group;
        var ownerMembership = createResult.Value.OwnerMembership;
        var submittedAt = new DateTime(2026, 6, 13, 12, 0, 0, DateTimeKind.Utc);
        var application = GroupService.SubmitJoinApplication(group, applicant.Id, submittedAt).Value;

        // Act
        await using (var context = CreateContext(databaseName))
        {
            context.Users.AddRange(owner, applicant);
            context.Groups.Add(group);
            context.GroupMemberships.Add(ownerMembership);
            context.GroupJoinApplications.Add(application);
            await context.SaveChangesAsync();
        }

        Group loadedGroup;
        await using (var context = CreateContext(databaseName))
        {
            loadedGroup = await context.Groups
                .Include(g => g.GroupMemberships)
                .Include(g => g.JoinApplications)
                .SingleAsync(g => g.Id == group.Id);
        }

        // Assert
        Assert.Equal("Test Org", loadedGroup.Name);
        Assert.Equal(GroupJoinPolicy.ApplicationRequired, loadedGroup.JoinPolicy);
        Assert.Single(loadedGroup.GroupMemberships);
        Assert.Single(loadedGroup.JoinApplications);
        Assert.Equal(GroupJoinApplicationStatus.Pending, loadedGroup.JoinApplications[0].Status);
    }

    [Fact]
    public async Task Event_Should_ReturnPersistedOrganizersAndAttendees_When_UserEventRelationsAreSaved()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();

        var user = UserService.ProvisionFromExternalIdentity(
            externalSubjectId: "auth0|test-subject-id",
            email: "test@example.com",
            emailVerified: true,
            profilePictureUrl: "https://example.com/pic.jpg",
            DefaultAvatarUrl,
            ServiceRole.User);
        user.Username = "testuser";

        var createResult = EventService.Create(EventTier.Small, "Test Event", user.Id, "/images/default-event-banner.png");
        var evt = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;

        EventService.Update(
            evt,
            new EventUpdatePatch
            {
                Title = "Test Event",
                Description = "Test description",
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddHours(2)
            },
            user.Id);

        // Act
        await using (var context = CreateContext(databaseName))
        {
            context.Users.Add(user);
            context.Events.Add(evt);
            context.EventOrganizers.Add(organizer);
            await context.SaveChangesAsync();

            context.EventAttendees.Add(new EventAttendee
            {
                EventId = evt.Id,
                ParticipantId = user.Id,
                Status = EventAttendeeStatus.Going,
                RegisteredAt = DateTime.UtcNow,
                Event = evt,
                Participant = user
            });

            await context.SaveChangesAsync();
        }

        Event loadedEvent;
        await using (var context = CreateContext(databaseName))
        {
            loadedEvent = await context.Events
                .Include(e => e.Organizers)
                .Include(e => e.Attendees)
                .SingleAsync(e => e.Id == evt.Id);
        }

        // Assert
        Assert.Single(loadedEvent.Organizers);
        Assert.Equal(user.Id, loadedEvent.Organizers[0].ParticipantId);

        Assert.Single(loadedEvent.Attendees);
        Assert.Equal(user.Id, loadedEvent.Attendees[0].ParticipantId);
        Assert.NotNull(loadedEvent.Attendees[0].RegisteredAt);
        Assert.Equal(EventAttendeeStatus.Going, loadedEvent.Attendees[0].Status);
    }

    private static ApplicationDbContext CreateContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
