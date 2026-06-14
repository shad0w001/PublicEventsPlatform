using Application.Abstractions.Authentication;
using Application.Events.ListMyEvents;
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

namespace ApplicationTests.Events.ListMyEvents;

public class ListMyEventsQueryHandlerTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";
    private const string DefaultGroupImageUrl = "/images/default-group.png";

    [Fact]
    public async Task ListMyEventsQueryHandler_Should_ReturnDraftAndPublished_When_UserHostsBoth()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|mine-self", "mine-self@example.com");
        var draftId = await SeedDraftEventAsync(databaseName, identity, "My Draft");
        var publishedId = await SeedPublishedEventAsync(databaseName, identity, "My Published");

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new ListMyEventsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal(draftId, result.Value[0].Id);
        Assert.Equal(EventStatus.Draft, result.Value[0].Status);
        Assert.Equal(publishedId, result.Value[1].Id);
        Assert.Equal(EventStatus.Published, result.Value[1].Status);
        Assert.False(result.Value[0].HostIsGroup);
    }

    [Fact]
    public async Task ListMyEventsQueryHandler_Should_IncludeBannerImageUrl_When_EventHasBanner()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|mine-banner", "banner-list@example.com");
        const string bannerUrl = "/uploads/events/card.webp";
        await SeedPublishedEventAsync(databaseName, identity, "Banner Event", bannerUrl: bannerUrl);

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new ListMyEventsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal(bannerUrl, result.Value[0].BannerImageUrl);
    }

    [Fact]
    public async Task ListMyEventsQueryHandler_Should_ReturnGroupHostedEvent_When_UserIsOrganizer()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|org-owner", "owner@example.com");
        var organizer = CreateUser("auth0|org-organizer", "org@example.com");
        var group = SeedGroupWithMember(databaseName, owner, organizer, GroupMemberRole.Organizer);

        var organizerIdentity = CreateVerifiedIdentity("auth0|org-organizer", "org@example.com");
        await SeedPublishedEventForHostAsync(databaseName, organizerIdentity, group.Id, "Org Event");

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, organizerIdentity);

        // Act
        var result = await handler.Handle(new ListMyEventsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.True(result.Value[0].HostIsGroup);
        Assert.Equal("Host Org", result.Value[0].HostDisplayName);
        Assert.Equal(group.Id, result.Value[0].HostParticipantId);
    }

    [Fact]
    public async Task ListMyEventsQueryHandler_Should_ExcludeGroupEvent_When_UserIsMemberOnly()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|member-owner", "mowner@example.com");
        var member = CreateUser("auth0|member-only", "member@example.com");
        var group = SeedGroupWithMember(databaseName, owner, member, GroupMemberRole.Member);

        var ownerIdentity = CreateVerifiedIdentity("auth0|member-owner", "mowner@example.com");
        await SeedPublishedEventForHostAsync(databaseName, ownerIdentity, group.Id, "Hidden Org Event");

        await using var context = CreateContext(databaseName);
        var memberIdentity = CreateVerifiedIdentity("auth0|member-only", "member@example.com");
        var handler = CreateHandler(context, memberIdentity);

        // Act
        var result = await handler.Handle(new ListMyEventsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task ListMyEventsQueryHandler_Should_IncludeEvent_When_CreatedByUserIdMatchesWithoutMembership()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|creator-owner", "cowner@example.com");
        var formerOrganizer = CreateUser("auth0|former-org", "former@example.com");
        var group = SeedGroupWithMember(databaseName, owner, formerOrganizer, GroupMemberRole.Organizer);

        var formerOrganizerIdentity = CreateVerifiedIdentity("auth0|former-org", "former@example.com");
        var eventId = await SeedPublishedEventForHostAsync(
            databaseName,
            formerOrganizerIdentity,
            group.Id,
            "Creator Event");

        await using (var seedContext = CreateContext(databaseName))
        {
            seedContext.GroupMemberships.RemoveRange(
                seedContext.GroupMemberships.Where(m => m.UserId == formerOrganizer.Id && m.GroupId == group.Id));
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, formerOrganizerIdentity);

        // Act
        var result = await handler.Handle(new ListMyEventsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal(eventId, result.Value[0].Id);
    }

    [Fact]
    public async Task ListMyEventsQueryHandler_Should_ExcludeSoftDeletedDraft()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|mine-deleted", "deleted@example.com");
        await SeedPublishedEventAsync(databaseName, identity, softDeleted: true);
        await SeedDraftEventAsync(databaseName, identity, "Visible Draft");

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new ListMyEventsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal(EventStatus.Draft, result.Value[0].Status);
    }

    [Fact]
    public async Task ListMyEventsQueryHandler_Should_ReturnEmailNotVerified_When_EmailIsUnverified()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = new FakeUserIdentityAccessor
        {
            IsAuthenticated = true,
            ExternalSubjectId = "auth0|mine-unverified",
            Email = "unverified@example.com",
            EmailVerified = false,
            ServiceRole = ServiceRole.User
        };

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new ListMyEventsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Users.EmailNotVerified", result.Error.Code);
    }

    [Fact]
    public async Task ListMyEventsQueryHandler_Should_ReturnEmptyList_When_UserHasNoEvents()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|mine-empty", "empty@example.com");

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new ListMyEventsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task ListMyEventsQueryHandler_Should_SortDraftBeforePublishedBeforeCancelled()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|mine-sort", "sort@example.com");
        await SeedPublishedEventAsync(databaseName, identity, "Published One", cancelled: true);
        await SeedDraftEventAsync(databaseName, identity, "Draft One");
        await SeedPublishedEventAsync(databaseName, identity, "Published Two");

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new ListMyEventsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Count);
        Assert.Equal(EventStatus.Draft, result.Value[0].Status);
        Assert.Equal(EventStatus.Published, result.Value[1].Status);
        Assert.Equal(EventStatus.Cancelled, result.Value[2].Status);
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

    private static async Task<Guid> SeedPublishedEventAsync(
        string databaseName,
        FakeUserIdentityAccessor identity,
        string title = "Published Event",
        bool cancelled = false,
        bool softDeleted = false,
        string? bannerUrl = null)
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
        MakePublishReadyViaUpdate(@event, categoryId, user.Id);

        if (bannerUrl is not null)
        {
            EventService.Update(@event, new EventUpdatePatch { BannerImageUrl = bannerUrl }, user.Id);
        }

        if (softDeleted)
        {
            EventService.SoftDelete(@event);
        }
        else
        {
            EventService.Publish(@event, categoryExists: true, recentPublishCount: 0, maxPublishesPerWeek: 100);

            if (cancelled)
            {
                EventService.Cancel(@event);
            }
        }

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
        return @event.Id;
    }

    private static async Task<Guid> SeedPublishedEventForHostAsync(
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

        EventService.Publish(@event, categoryExists: true, recentPublishCount: 0, maxPublishesPerWeek: 100);

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
        Guid actingUserId)
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
                    Date = start,
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

    private static ListMyEventsQueryHandler CreateHandler(
        ApplicationDbContext context,
        IUserIdentityAccessor identity)
    {
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));

        return new ListMyEventsQueryHandler(
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
        public ServiceRole ServiceRole { get; init; }
        public string? ProfilePictureUrl { get; init; }
    }
}
