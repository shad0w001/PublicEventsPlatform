using Application.Notifications;
using Application.Notifications.Services;
using Domain.Groups;
using Domain.Groups.Services;
using Domain.Users;
using Domain.Users.Services;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApplicationTests.Notifications;

public class NotificationRecipientServiceTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";
    private const string DefaultGroupImageUrl = "/images/default-group.png";

    [Fact]
    public async Task GetUserEmailAsync_Should_ReturnEmail_When_UserExists()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var user = CreateUser("auth0|notify-user", "notify-user@example.com");
        await SeedUserAsync(databaseName, user);
        var service = CreateService(databaseName);

        // Act
        var email = await service.GetUserEmailAsync(user.Id, CancellationToken.None);

        // Assert
        Assert.Equal("notify-user@example.com", email);
    }

    [Fact]
    public async Task GetUserEmailAsync_Should_ReturnNull_When_UserDoesNotExist()
    {
        // Arrange
        var service = CreateService(Guid.NewGuid().ToString());

        // Act
        var email = await service.GetUserEmailAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.Null(email);
    }

    [Fact]
    public async Task GetOrganizerPlusEmailsAsync_Should_ReturnOrganizerAndAdminAndOwner_When_GroupHasMixedRoles()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|org-owner", "owner@example.com");
        var organizer = CreateUser("auth0|org-organizer", "organizer@example.com");
        var moderator = CreateUser("auth0|org-moderator", "moderator@example.com");
        var member = CreateUser("auth0|org-member", "member@example.com");
        var group = SeedGroup(
            databaseName,
            owner,
            [
                (organizer, GroupMemberRole.Organizer),
                (moderator, GroupMemberRole.Moderator),
                (member, GroupMemberRole.Member)
            ]);
        var service = CreateService(databaseName);

        // Act
        var emails = await service.GetOrganizerPlusEmailsAsync(group.Id, CancellationToken.None);

        // Assert
        Assert.Equal(2, emails.Count);
        Assert.Contains("owner@example.com", emails);
        Assert.Contains("organizer@example.com", emails);
        Assert.DoesNotContain("moderator@example.com", emails);
        Assert.DoesNotContain("member@example.com", emails);
    }

    [Fact]
    public async Task GetApplicationReviewerEmailsAsync_Should_ReturnModeratorAndAdminAndOwner_When_GroupHasMixedRoles()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|review-owner", "review-owner@example.com");
        var organizer = CreateUser("auth0|review-organizer", "review-organizer@example.com");
        var moderator = CreateUser("auth0|review-moderator", "review-moderator@example.com");
        var group = SeedGroup(
            databaseName,
            owner,
            [
                (organizer, GroupMemberRole.Organizer),
                (moderator, GroupMemberRole.Moderator)
            ]);
        var service = CreateService(databaseName);

        // Act
        var emails = await service.GetApplicationReviewerEmailsAsync(group.Id, CancellationToken.None);

        // Assert
        Assert.Equal(2, emails.Count);
        Assert.Contains("review-owner@example.com", emails);
        Assert.Contains("review-moderator@example.com", emails);
        Assert.DoesNotContain("review-organizer@example.com", emails);
    }

    [Fact]
    public async Task GetOrganizerPlusEmailsAsync_Should_ReturnEmpty_When_GroupHasNoQualifyingMembers()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|mod-only-owner", "mod-only-owner@example.com");
        var moderator = CreateUser("auth0|mod-only-moderator", "mod-only-moderator@example.com");
        var group = SeedGroup(
            databaseName,
            owner,
            [(moderator, GroupMemberRole.Moderator)]);
        var service = CreateService(databaseName);

        // Act
        var emails = await service.GetOrganizerPlusEmailsAsync(group.Id, CancellationToken.None);

        // Assert
        Assert.Single(emails);
        Assert.Contains("mod-only-owner@example.com", emails);
    }

    [Fact]
    public async Task ResolveParticipantEmailsAsync_Should_ReturnUserEmail_When_ParticipantIsUser()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var user = CreateUser("auth0|resolve-user", "resolve-user@example.com");
        await SeedUserAsync(databaseName, user);
        var service = CreateService(databaseName);

        // Act
        var emails = await service.ResolveParticipantEmailsAsync(user.Id, CancellationToken.None);

        // Assert
        Assert.Single(emails);
        Assert.Equal("resolve-user@example.com", emails[0]);
    }

    [Fact]
    public async Task ResolveParticipantEmailsAsync_Should_ReturnOrganizerPlusEmails_When_ParticipantIsGroup()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|resolve-group-owner", "resolve-group-owner@example.com");
        var organizer = CreateUser("auth0|resolve-group-organizer", "resolve-group-organizer@example.com");
        var group = SeedGroup(
            databaseName,
            owner,
            [(organizer, GroupMemberRole.Organizer)]);
        var service = CreateService(databaseName);

        // Act
        var emails = await service.ResolveParticipantEmailsAsync(group.Id, CancellationToken.None);

        // Assert
        Assert.Equal(2, emails.Count);
        Assert.Contains("resolve-group-owner@example.com", emails);
        Assert.Contains("resolve-group-organizer@example.com", emails);
    }

    [Fact]
    public async Task ResolveParticipantEmailsAsync_Should_ReturnEmpty_When_ParticipantIsUnknown()
    {
        // Arrange
        var service = CreateService(Guid.NewGuid().ToString());

        // Act
        var emails = await service.ResolveParticipantEmailsAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.Empty(emails);
    }

    [Fact]
    public void NotificationLinkBuilder_Should_BuildSpaUrls_When_BaseUrlHasTrailingSlash()
    {
        // Arrange
        var eventId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var ticketId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var groupId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var orderId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var builder = new NotificationLinkBuilder(Options.Create(new NotificationsOptions
        {
            PublicAppBaseUrl = "http://localhost:5173/"
        }));

        // Act & Assert
        Assert.Equal($"http://localhost:5173/events/{eventId}", builder.Event(eventId));
        Assert.Equal($"http://localhost:5173/tickets/{ticketId}", builder.Ticket(ticketId));
        Assert.Equal($"http://localhost:5173/groups/{groupId}", builder.Group(groupId));
        Assert.Equal($"http://localhost:5173/orders/{orderId}", builder.Order(orderId));
    }

    private static async Task SeedUserAsync(string databaseName, User user)
    {
        await using var seedContext = CreateContext(databaseName);
        seedContext.Users.Add(user);
        await seedContext.SaveChangesAsync();
    }

    private static Group SeedGroup(
        string databaseName,
        User owner,
        IReadOnlyList<(User User, GroupMemberRole Role)> additionalMembers)
    {
        var createResult = GroupService.Create(
            "Notify Org",
            "",
            GroupJoinPolicy.Open,
            owner.Id,
            DefaultGroupImageUrl);
        var group = createResult.Value.Group;

        using var seedContext = CreateContext(databaseName);
        seedContext.Users.Add(owner);
        seedContext.Groups.Add(group);
        seedContext.GroupMemberships.Add(createResult.Value.OwnerMembership);

        foreach (var (user, role) in additionalMembers)
        {
            if (user.Id == owner.Id)
            {
                continue;
            }

            seedContext.Users.Add(user);
            seedContext.GroupMemberships.Add(GroupMembership.Create(group.Id, user.Id, role));
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

    private static NotificationRecipientService CreateService(string databaseName) =>
        new(CreateContext(databaseName));

    private static ApplicationDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new ApplicationDbContext(options);
    }
}
