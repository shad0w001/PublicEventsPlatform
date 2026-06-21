using Application.Abstractions.Authentication;
using Application.Events;
using Application.Events.GetEvent;
using Application.Events.PublishEvent;
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
using System.Text.Json;

namespace ApplicationTests.Events.GetEvent;

using ApplicationTests.Events;

public class GetEventQueryHandlerTests
{
    private const string DefaultAvatarUrl = "/images/default-avatar.png";
    private const string DefaultGroupImageUrl = "/images/default-group.png";

    [Fact]
    public async Task GetEventQueryHandler_Should_ReturnPublicOnly_When_AnonymousGetsPublishedEvent()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|pub-view", "viewer-host@example.com");
        var eventId = await SeedPublishedEventAsync(databaseName, identity, username: "eventhost");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, new FakeUserIdentityAccessor { IsAuthenticated = false });

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Public);
        Assert.Null(result.Value.EditDetail);
        Assert.False(result.Value.CanEdit);
        Assert.Equal(EventStatus.Published, result.Value.Public!.Status);
        Assert.Equal("eventhost", result.Value.Public.HostDisplayName);
        Assert.False(result.Value.Public.HostIsGroup);
        Assert.Equal("Music", result.Value.Public.CategoryName);
    }

    [Fact]
    public async Task GetEventQueryHandler_Should_EmbedTicketTypes_When_PaidPublishedEvent()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|pub-ticket-types", "ticket-types@example.com");
        var eventId = await SeedPublishedPaidEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, new FakeUserIdentityAccessor { IsAuthenticated = false });

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Public);
        Assert.Equal(AdmissionType.Paid, result.Value.Public!.AdmissionType);
        Assert.Single(result.Value.Public.TicketTypes);
        Assert.Equal("General Admission", result.Value.Public.TicketTypes[0].Name);
        Assert.Equal(2500, result.Value.Public.TicketTypes[0].PriceCents);
        Assert.Null(result.Value.Public.RsvpSummary);
    }

    [Fact]
    public async Task GetEventQueryHandler_Should_ReturnEmptyTicketTypes_When_FreePublishedEvent()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|free-no-tickets", "free-no-tickets@example.com");
        var eventId = await SeedPublishedEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, new FakeUserIdentityAccessor { IsAuthenticated = false });

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Public);
        Assert.Equal(AdmissionType.Free, result.Value.Public!.AdmissionType);
        Assert.Empty(result.Value.Public.TicketTypes);
    }

    [Fact]
    public async Task GetEventQueryHandler_Should_EmbedTicketTypesOnEditDetail_When_EditorGetsPaidDraft()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|draft-ticket-types", "draft-tickets@example.com");
        var eventId = await SeedPaidDraftWithTicketTypeAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Public);
        Assert.NotNull(result.Value.EditDetail);
        Assert.True(result.Value.CanEdit);
        Assert.Single(result.Value.EditDetail!.TicketTypes);
        Assert.Equal("General Admission", result.Value.EditDetail.TicketTypes[0].Name);
        Assert.Null(result.Value.EditDetail.RsvpSummary);
    }

    [Fact]
    public async Task GetEventQueryHandler_Should_ReturnCancelledStatus_When_AnonymousGetsCancelledEvent()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|cancel-view", "cancel-host@example.com");
        var eventId = await SeedPublishedEventAsync(databaseName, identity, cancelled: true);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, new FakeUserIdentityAccessor { IsAuthenticated = false });

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Public);
        Assert.Equal(EventStatus.Cancelled, result.Value.Public!.Status);
        Assert.Null(result.Value.EditDetail);
        Assert.False(result.Value.CanEdit);
    }

    [Fact]
    public async Task GetEventQueryHandler_Should_ReturnNotFound_When_AnonymousGetsDraft()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|draft-anon", "draft@example.com");
        var eventId = await SeedDraftEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, new FakeUserIdentityAccessor { IsAuthenticated = false });

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task GetEventQueryHandler_Should_ReturnNotFound_When_NonEditorGetsDraft()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var ownerIdentity = CreateVerifiedIdentity("auth0|draft-owner", "owner@example.com");
        var eventId = await SeedDraftEventAsync(databaseName, ownerIdentity);

        var strangerIdentity = CreateVerifiedIdentity("auth0|draft-stranger", "stranger@example.com");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, strangerIdentity);

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task GetEventQueryHandler_Should_ReturnEditDetailOnly_When_EditorGetsDraft()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|draft-editor", "editor@example.com");
        var eventId = await SeedDraftEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Public);
        Assert.NotNull(result.Value.EditDetail);
        Assert.True(result.Value.CanEdit);
        Assert.Equal(EventStatus.Draft, result.Value.EditDetail!.Status);
        Assert.Equal(eventId, result.Value.EditDetail.Id);
    }

    [Fact]
    public async Task GetEventQueryHandler_Should_ReturnEditDetailOnly_When_EditorGetsPublishedEvent()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|pub-editor", "pub-editor@example.com");
        var eventId = await SeedPublishedEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Public);
        Assert.NotNull(result.Value.EditDetail);
        Assert.True(result.Value.CanEdit);
        Assert.Equal(eventId, result.Value.EditDetail!.Id);
        Assert.Equal(EventStatus.Published, result.Value.EditDetail.Status);
        Assert.Equal("Music", result.Value.EditDetail.CategoryName);
    }

    [Fact]
    public async Task GetEventQueryHandler_Should_ReturnPublicOnly_When_NonEditorGetsPublishedEvent()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var ownerIdentity = CreateVerifiedIdentity("auth0|pub-owner", "owner@example.com");
        var eventId = await SeedPublishedEventAsync(databaseName, ownerIdentity);

        var strangerIdentity = CreateVerifiedIdentity("auth0|pub-stranger", "stranger@example.com");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, strangerIdentity);

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Public);
        Assert.Null(result.Value.EditDetail);
        Assert.False(result.Value.CanEdit);
    }

    [Fact]
    public async Task GetEventQueryHandler_Should_ReturnEditDetailWithCanEditFalse_When_EditorGetsCancelledEvent()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|cancel-editor", "cancel-editor@example.com");
        var eventId = await SeedPublishedEventAsync(databaseName, identity, cancelled: true);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Public);
        Assert.NotNull(result.Value.EditDetail);
        Assert.False(result.Value.CanEdit);
        Assert.Equal(EventStatus.Cancelled, result.Value.EditDetail!.Status);
        Assert.Equal(eventId, result.Value.EditDetail.Id);
    }

    [Fact]
    public async Task GetEventQueryHandler_Should_ReturnPublicOnly_When_FormerGroupOrganizerGetsPublishedEvent()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|former-get-owner", "fget-owner@example.com");
        var formerOrganizer = CreateUser("auth0|former-get-org", "fget-org@example.com");
        var group = SeedGroupWithMember(databaseName, owner, formerOrganizer, GroupMemberRole.Organizer);

        var formerOrganizerIdentity = CreateVerifiedIdentity("auth0|former-get-org", "fget-org@example.com");
        var eventId = await SeedPublishedEventForHostAsync(
            databaseName,
            formerOrganizerIdentity,
            group.Id,
            "Former Org Event");

        await using (var seedContext = CreateContext(databaseName))
        {
            seedContext.GroupMemberships.RemoveRange(
                seedContext.GroupMemberships.Where(m => m.UserId == formerOrganizer.Id && m.GroupId == group.Id));
            await seedContext.SaveChangesAsync(CancellationToken.None);
        }

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, formerOrganizerIdentity);

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Public);
        Assert.Null(result.Value.EditDetail);
        Assert.False(result.Value.CanEdit);
        Assert.Equal("Host Org", result.Value.Public!.HostDisplayName);
    }

    [Fact]
    public async Task GetEventQueryHandler_Should_ReturnNotFound_When_EventIsSoftDeleted()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|deleted-get", "deleted@example.com");
        var eventId = await SeedPublishedEventAsync(databaseName, identity, softDeleted: true);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Events.Deleted", result.Error.Code);
    }

    [Fact]
    public async Task GetEventQueryHandler_Should_ReturnGroupHostDisplayName_When_EventHostedByGroup()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|group-host-owner", "gowner@example.com");
        var organizer = CreateUser("auth0|group-host-org", "gorg@example.com");
        var group = SeedGroupWithMember(databaseName, owner, organizer, GroupMemberRole.Organizer);

        var organizerIdentity = CreateVerifiedIdentity("auth0|group-host-org", "gorg@example.com");
        var eventId = await SeedPublishedEventForHostAsync(
            databaseName,
            organizerIdentity,
            group.Id,
            "Group Event");

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, new FakeUserIdentityAccessor { IsAuthenticated = false });

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Public);
        Assert.Equal("Host Org", result.Value.Public!.HostDisplayName);
        Assert.True(result.Value.Public.HostIsGroup);
    }

    [Fact]
    public async Task GetEventQueryHandler_Should_ReturnSegmentTimesOnPublic_When_PublishedEventHasBoundedSegments()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|public-segments", "pubseg@example.com");
        var segmentStart = new DateTime(2026, 7, 1, 19, 0, 0, DateTimeKind.Utc);
        var segmentEnd = new DateTime(2026, 7, 1, 21, 0, 0, DateTimeKind.Utc);
        var eventId = await SeedPublishedEventAsync(
            databaseName,
            identity,
            segmentStartsAt: segmentStart,
            segmentEndsAt: segmentEnd);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, new FakeUserIdentityAccessor { IsAuthenticated = false });

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Public);
        Assert.Single(result.Value.Public!.Locations);
        Assert.Equal(segmentStart, result.Value.Public.Locations[0].StartsAt);
        Assert.Equal(segmentEnd, result.Value.Public.Locations[0].EndsAt);
    }

    [Fact]
    public async Task GetEventQueryHandler_Should_ReturnSegmentTimesOnEditDetail_When_EditorGetsDraft()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|draft-segments", "draftseg@example.com");
        var segmentStart = new DateTime(2026, 8, 1, 18, 0, 0, DateTimeKind.Utc);
        var segmentEnd = new DateTime(2026, 8, 1, 20, 0, 0, DateTimeKind.Utc);
        var eventId = await SeedDraftEventWithLocationsAsync(
            databaseName,
            identity,
            segmentStart,
            segmentEnd);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.EditDetail);
        Assert.Single(result.Value.EditDetail!.Locations);
        Assert.Equal(segmentStart, result.Value.EditDetail.Locations[0].StartsAt);
        Assert.Equal(segmentEnd, result.Value.EditDetail.Locations[0].EndsAt);
    }

    [Fact]
    public async Task GetEventQueryHandler_Should_ReturnNullSegmentTimes_When_LocationUsesEventWindow()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|null-segments", "nullseg@example.com");
        var eventId = await SeedPublishedEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, new FakeUserIdentityAccessor { IsAuthenticated = false });

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Public);
        Assert.Single(result.Value.Public!.Locations);
        Assert.Null(result.Value.Public.Locations[0].StartsAt);
        Assert.Null(result.Value.Public.Locations[0].EndsAt);
    }

    [Fact]
    public async Task GetEventQueryHandler_Should_ReturnPluginsSortedNewestFirst_When_AnonymousGetsPublishedBigEvent()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|pub-plugins", "plugins@example.com");
        var eventId = await SeedBigPublishedEventWithPluginsAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, new FakeUserIdentityAccessor { IsAuthenticated = false });

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Public);
        Assert.Equal(2, result.Value.Public!.Plugins.Count);
        Assert.Equal(PluginConstants.CodeLinks, result.Value.Public.Plugins[0].Code);
        Assert.Equal(PluginConstants.CodeFaq, result.Value.Public.Plugins[1].Code);
        Assert.True(result.Value.Public.Plugins[0].AttachedAt >= result.Value.Public.Plugins[1].AttachedAt);
    }

    [Fact]
    public async Task GetEventQueryHandler_Should_ReturnPluginsOnEditDetail_When_EditorGetsDraftWithPlugin()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|draft-plugin", "draftplugin@example.com");
        var eventId = await SeedBigDraftEventWithFaqPluginAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.EditDetail);
        Assert.Single(result.Value.EditDetail!.Plugins);
        Assert.Equal(PluginConstants.CodeFaq, result.Value.EditDetail.Plugins[0].Code);
        Assert.Contains(PluginConstants.DataKeyEntries, result.Value.EditDetail.Plugins[0].Data.Keys);
    }

    [Fact]
    public async Task GetEventQueryHandler_Should_ReturnPluginsWithCanEditFalse_When_CancelledPublishedEvent()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|cancel-plugin", "cancelplugin@example.com");
        var eventId = await SeedBigPublishedEventWithPluginsAsync(databaseName, identity, cancelled: true);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, new FakeUserIdentityAccessor { IsAuthenticated = false });

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Public);
        Assert.Equal(2, result.Value.Public!.Plugins.Count);
        Assert.False(result.Value.CanEdit);
    }

    [Fact]
    public async Task GetEventQueryHandler_Should_ReturnEmptyPlugins_When_PublishedEventHasNoPlugins()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|no-plugins", "noplugins@example.com");
        var eventId = await SeedPublishedEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, new FakeUserIdentityAccessor { IsAuthenticated = false });

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Public);
        Assert.Empty(result.Value.Public!.Plugins);
    }

    [Fact]
    public async Task GetEventQueryHandler_Should_ReturnEmptyPlugins_When_SmallTierPublishedEvent()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|small-plugins", "small@example.com");
        var eventId = await SeedPublishedEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, new FakeUserIdentityAccessor { IsAuthenticated = false });

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Public);
        Assert.Equal(EventTier.Small, result.Value.Public!.Tier);
        Assert.Empty(result.Value.Public.Plugins);
    }

    [Fact]
    public async Task GetEventQueryHandler_Should_ReturnRsvpCountsOnly_When_AnonymousGetsFreePublishedEvent()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var hostIdentity = CreateVerifiedIdentity("auth0|rsvp-count-host", "rsvp-count-host@example.com");
        var eventId = await SeedPublishedEventWithAttendeesAsync(
            databaseName,
            hostIdentity,
            extraUserStatuses:
            [
                EventAttendeeStatus.Going,
                EventAttendeeStatus.Going,
                EventAttendeeStatus.Interested,
                EventAttendeeStatus.NotGoing
            ]);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, new FakeUserIdentityAccessor { IsAuthenticated = false });

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Public);
        Assert.NotNull(result.Value.Public!.RsvpSummary);
        Assert.Equal(2, result.Value.Public.RsvpSummary!.GoingCount);
        Assert.Equal(1, result.Value.Public.RsvpSummary.InterestedCount);
        Assert.Equal(3, result.Value.Public.RsvpSummary.ResponseCount);
        Assert.Null(result.Value.Public.RsvpSummary.MyStatuses);
    }

    [Fact]
    public async Task GetEventQueryHandler_Should_ReturnMyStatuses_When_AuthenticatedUserHasSelfRsvp()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var hostIdentity = CreateVerifiedIdentity("auth0|rsvp-self-host", "rsvp-self-host@example.com");
        var attendeeIdentity = CreateVerifiedIdentity("auth0|rsvp-self-attendee", "rsvp-self-attendee@example.com");
        var eventId = await SeedPublishedEventWithAttendeesAsync(
            databaseName,
            hostIdentity,
            attendees: [],
            extraAttendeeUserIdentity: attendeeIdentity,
            extraAttendeeStatus: EventAttendeeStatus.Interested);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, attendeeIdentity);

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Public);
        Assert.NotNull(result.Value.Public!.RsvpSummary);
        Assert.Single(result.Value.Public.RsvpSummary!.MyStatuses!);
        var myStatus = result.Value.Public.RsvpSummary.MyStatuses![0];
        var attendeeUser = await context.Users.SingleAsync(u => u.Email == "rsvp-self-attendee@example.com");
        Assert.Equal(attendeeUser.Id, myStatus.ParticipantId);
        Assert.False(myStatus.ParticipantIsGroup);
        Assert.Equal(EventAttendeeStatus.Interested, myStatus.Status);
    }

    [Fact]
    public async Task GetEventQueryHandler_Should_ReturnGroupMyStatus_When_OrganizerPlusHasGroupRsvpRow()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var owner = CreateUser("auth0|rsvp-group-owner", "rsvp-group-owner@example.com");
        var organizer = CreateUser("auth0|rsvp-group-organizer", "rsvp-group-organizer@example.com");
        var group = SeedGroupWithMember(databaseName, owner, organizer, GroupMemberRole.Organizer);
        var hostIdentity = CreateVerifiedIdentity("auth0|rsvp-group-host", "rsvp-group-host@example.com");
        var eventId = await SeedPublishedEventWithAttendeesAsync(
            databaseName,
            hostIdentity,
            attendees: [(group.Id, EventAttendeeStatus.Going)]);
        var organizerIdentity = CreateVerifiedIdentity("auth0|rsvp-group-organizer", "rsvp-group-organizer@example.com");
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, organizerIdentity);

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Public);
        Assert.NotNull(result.Value.Public!.RsvpSummary);
        Assert.Single(result.Value.Public.RsvpSummary!.MyStatuses!);
        var myStatus = result.Value.Public.RsvpSummary.MyStatuses![0];
        Assert.Equal(group.Id, myStatus.ParticipantId);
        Assert.True(myStatus.ParticipantIsGroup);
        Assert.Equal(EventAttendeeStatus.Going, myStatus.Status);
    }

    [Fact]
    public async Task GetEventQueryHandler_Should_ReturnNullRsvpSummary_When_PaidPublishedEvent()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|rsvp-paid", "rsvp-paid@example.com");
        var eventId = await SeedPublishedEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var paidEvent = await context.Events.SingleAsync(e => e.Id == eventId);
        paidEvent.AdmissionType = AdmissionType.Paid;
        await context.SaveChangesAsync(CancellationToken.None);
        var handler = CreateHandler(context, new FakeUserIdentityAccessor { IsAuthenticated = false });

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Public);
        Assert.Null(result.Value.Public!.RsvpSummary);
    }

    [Fact]
    public async Task GetEventQueryHandler_Should_ReturnNullRsvpSummary_When_EditorGetsDraft()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|rsvp-draft", "rsvp-draft@example.com");
        var eventId = await SeedDraftEventAsync(databaseName, identity);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, identity);

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.EditDetail);
        Assert.Null(result.Value.EditDetail!.RsvpSummary);
    }

    [Fact]
    public async Task GetEventQueryHandler_Should_ReturnFrozenRsvpCounts_When_CancelledFreeEvent()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var hostIdentity = CreateVerifiedIdentity("auth0|rsvp-cancel-host", "rsvp-cancel-host@example.com");
        var attendeeIdentity = CreateVerifiedIdentity("auth0|rsvp-cancel-attendee", "rsvp-cancel-attendee@example.com");
        var eventId = await SeedPublishedEventWithAttendeesAsync(
            databaseName,
            hostIdentity,
            attendees: [],
            extraAttendeeUserIdentity: attendeeIdentity,
            extraAttendeeStatus: EventAttendeeStatus.Going,
            cancelled: true);
        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, attendeeIdentity);

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Public);
        Assert.Equal(EventStatus.Cancelled, result.Value.Public!.Status);
        Assert.NotNull(result.Value.Public.RsvpSummary);
        Assert.Equal(1, result.Value.Public.RsvpSummary!.GoingCount);
        Assert.NotNull(result.Value.Public.RsvpSummary.MyStatuses);
    }

    [Fact]
    public async Task GetEventQueryHandler_Should_IncludeHostInGoingCount_When_FreeEventPublishedViaHandler()
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var identity = CreateVerifiedIdentity("auth0|rsvp-publish-host", "rsvp-publish-host@example.com");
        var eventId = await SeedPublishReadyDraftAsync(databaseName, identity);
        await using var publishContext = CreateContext(databaseName);
        var publishHandler = new PublishEventCommandHandler(
            publishContext,
            new CurrentUserService(
                publishContext,
                identity,
                Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl })),
            identity,
            new EventAccessService(publishContext),
            new EventVenueConflictService(publishContext),
            Options.Create(new EventOptions { MaxPublishesPerHostPerWeek = 6 }));
        await publishHandler.Handle(new PublishEventCommand(eventId), CancellationToken.None);

        await using var context = CreateContext(databaseName);
        var handler = CreateHandler(context, new FakeUserIdentityAccessor { IsAuthenticated = false });

        // Act
        var result = await handler.Handle(new GetEventQuery(eventId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Public);
        Assert.NotNull(result.Value.Public!.RsvpSummary);
        Assert.Equal(1, result.Value.Public.RsvpSummary!.GoingCount);
        Assert.Equal(0, result.Value.Public.RsvpSummary.InterestedCount);
        Assert.Equal(1, result.Value.Public.RsvpSummary.ResponseCount);
    }

    private static async Task<Guid> SeedPublishReadyDraftAsync(
        string databaseName,
        FakeUserIdentityAccessor identity)
    {
        await using var context = CreateContext(databaseName);
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var user = (await currentUserService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var createResult = EventService.Create(EventTier.Small, "Publish Ready Draft", user.Id, EventTestConstants.DefaultBannerUrl);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;
        var categoryId = SeedCategoryInContext(context);
        MakePublishReadyViaUpdate(@event, categoryId, user.Id);

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
        return @event.Id;
    }

    private static async Task<Guid> SeedPublishedEventWithAttendeesAsync(
        string databaseName,
        FakeUserIdentityAccessor hostIdentity,
        IReadOnlyList<(Guid ParticipantId, EventAttendeeStatus Status)>? attendees = null,
        IReadOnlyList<EventAttendeeStatus>? extraUserStatuses = null,
        FakeUserIdentityAccessor? extraAttendeeUserIdentity = null,
        EventAttendeeStatus extraAttendeeStatus = EventAttendeeStatus.Going,
        bool cancelled = false)
    {
        await using var context = CreateContext(databaseName);
        var currentUserService = new CurrentUserService(
            context,
            hostIdentity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var hostUser = (await currentUserService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var createResult = EventService.Create(EventTier.Small, "RSVP Summary Event", hostUser.Id, EventTestConstants.DefaultBannerUrl);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;

        var categoryId = SeedCategoryInContext(context);
        MakePublishReadyViaUpdate(@event, categoryId, hostUser.Id);
        EventService.Publish(@event, categoryExists: true, recentPublishCount: 0, maxPublishesPerWeek: 100);

        if (cancelled)
        {
            EventService.Cancel(@event);
        }

        var now = DateTime.UtcNow;
        if (extraUserStatuses is not null)
        {
            for (var i = 0; i < extraUserStatuses.Count; i++)
            {
                var extraUser = CreateUser($"auth0|rsvp-extra-{Guid.NewGuid():N}", $"rsvp-extra-{i}@example.com");
                context.Users.Add(extraUser);
                @event.Attendees.Add(new EventAttendee
                {
                    EventId = @event.Id,
                    ParticipantId = extraUser.Id,
                    Status = extraUserStatuses[i],
                    RegisteredAt = now
                });
            }
        }

        if (extraAttendeeUserIdentity is not null)
        {
            var extraService = new CurrentUserService(
                context,
                extraAttendeeUserIdentity,
                Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
            var extraUser = (await extraService.GetOrProvisionAsync(CancellationToken.None)).Value;
            @event.Attendees.Add(new EventAttendee
            {
                EventId = @event.Id,
                ParticipantId = extraUser.Id,
                Status = extraAttendeeStatus,
                RegisteredAt = now
            });
        }

        foreach (var (participantId, status) in attendees ?? [])
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

    private static async Task<Guid> SeedBigDraftEventWithFaqPluginAsync(
        string databaseName,
        FakeUserIdentityAccessor identity)
    {
        await using var context = CreateContext(databaseName);
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var user = (await currentUserService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var createResult = EventService.Create(EventTier.Big, "Big Draft With Plugin", user.Id, EventTestConstants.DefaultBannerUrl);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;

        var faqPlugin = new Plugin
        {
            Code = PluginConstants.CodeFaq,
            Name = "FAQ",
            Description = "FAQ plugin",
            Version = "1.0.0"
        };
        context.Plugins.Add(faqPlugin);
        PluginService.Attach(@event, faqPlugin, ValidFaqData());

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
        return @event.Id;
    }

    private static async Task<Guid> SeedBigPublishedEventWithPluginsAsync(
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

        var createResult = EventService.Create(EventTier.Big, "Big Published With Plugins", user.Id, EventTestConstants.DefaultBannerUrl);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;

        var categoryId = SeedCategoryInContext(context);
        MakePublishReadyViaUpdate(@event, categoryId, user.Id);

        var faqPlugin = new Plugin
        {
            Code = PluginConstants.CodeFaq,
            Name = "FAQ",
            Description = "FAQ plugin",
            Version = "1.0.0"
        };
        var linksPlugin = new Plugin
        {
            Code = PluginConstants.CodeLinks,
            Name = "Important Links",
            Description = "Links plugin",
            Version = "1.0.0"
        };
        context.Plugins.AddRange(faqPlugin, linksPlugin);

        PluginService.Attach(@event, faqPlugin, ValidFaqData());
        await Task.Delay(5);
        PluginService.Attach(@event, linksPlugin, ValidLinksData());

        EventService.Publish(@event, categoryExists: true, recentPublishCount: 0, maxPublishesPerWeek: 100);
        if (cancelled)
        {
            EventService.Cancel(@event);
        }

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
        return @event.Id;
    }

    private static Dictionary<string, string?> ValidFaqData()
    {
        var entries = JsonSerializer.Serialize(new[]
        {
            new { question = "What time?", answer = "At 6 PM." }
        });

        return new Dictionary<string, string?> { [PluginConstants.DataKeyEntries] = entries };
    }

    private static Dictionary<string, string?> ValidLinksData()
    {
        var links = JsonSerializer.Serialize(new[]
        {
            new { label = "Website", url = "https://example.com" }
        });

        return new Dictionary<string, string?> { [PluginConstants.DataKeyLinks] = links };
    }

    private static async Task<Guid> SeedDraftEventWithLocationsAsync(
        string databaseName,
        FakeUserIdentityAccessor identity,
        DateTime segmentStartsAt,
        DateTime segmentEndsAt)
    {
        await using var context = CreateContext(databaseName);
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var user = (await currentUserService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var createResult = EventService.Create(EventTier.Small, "Draft With Segments", user.Id, EventTestConstants.DefaultBannerUrl);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;

        var categoryId = SeedCategoryInContext(context);
        var eventStart = new DateTime(2026, 8, 1, 17, 0, 0, DateTimeKind.Utc);
        var eventEnd = new DateTime(2026, 8, 1, 22, 0, 0, DateTimeKind.Utc);
        var patch = new EventUpdatePatch
        {
            Description = "Draft description",
            CategoryId = categoryId,
            StartTime = eventStart,
            EndTime = eventEnd,
            TimeZoneId = "Europe/Sofia",
            Locations =
            [
                new EventLocation
                {
                    Name = "Hall",
                    StartsAt = segmentStartsAt,
                    EndsAt = segmentEndsAt,
                    Kind = EventLocationKind.Physical,
                    Address = "1 Main St",
                    City = "Sofia"
                }
            ]
        };

        var updateResult = EventService.Update(@event, patch, user.Id);
        if (updateResult.IsFailure)
        {
            throw new InvalidOperationException(updateResult.Error.Code);
        }

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
        return @event.Id;
    }

    private static async Task<Guid> SeedPublishedPaidEventAsync(
        string databaseName,
        FakeUserIdentityAccessor identity)
    {
        await using var context = CreateContext(databaseName);
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var user = (await currentUserService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var createResult = EventService.Create(EventTier.Small, "Published Paid", user.Id, EventTestConstants.DefaultBannerUrl);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;

        var categoryId = SeedCategoryInContext(context);
        MakePublishReadyViaUpdate(@event, categoryId, user.Id, admissionType: AdmissionType.Paid);
        TicketTypeService.Create(@event, "General Admission", "Standard entry", 2500, 100);
        EventService.Publish(@event, categoryExists: true, recentPublishCount: 0, maxPublishesPerWeek: 100);

        context.Events.Add(@event);
        context.EventOrganizers.Add(organizer);
        await context.SaveChangesAsync(CancellationToken.None);
        return @event.Id;
    }

    private static async Task<Guid> SeedPaidDraftWithTicketTypeAsync(
        string databaseName,
        FakeUserIdentityAccessor identity)
    {
        await using var context = CreateContext(databaseName);
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var user = (await currentUserService.GetOrProvisionAsync(CancellationToken.None)).Value;

        var createResult = EventService.Create(EventTier.Small, "Paid Draft", user.Id, EventTestConstants.DefaultBannerUrl);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;

        var categoryId = SeedCategoryInContext(context);
        MakePublishReadyViaUpdate(@event, categoryId, user.Id, admissionType: AdmissionType.Paid);
        TicketTypeService.Create(@event, "General Admission", "Standard entry", 2500, 100);

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

        var createResult = EventService.Create(EventTier.Small, "Draft Event", user.Id, EventTestConstants.DefaultBannerUrl);
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
        string? username = null,
        bool cancelled = false,
        bool softDeleted = false,
        DateTime? segmentStartsAt = null,
        DateTime? segmentEndsAt = null)
    {
        await using var context = CreateContext(databaseName);
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));
        var user = (await currentUserService.GetOrProvisionAsync(CancellationToken.None)).Value;

        if (username is not null)
        {
            user.Username = username;
        }

        var createResult = EventService.Create(EventTier.Small, "Published Event", user.Id, EventTestConstants.DefaultBannerUrl);
        var @event = createResult.Value.Event;
        var organizer = createResult.Value.Organizer;

        var categoryId = SeedCategoryInContext(context);
        MakePublishReadyViaUpdate(@event, categoryId, user.Id, segmentStartsAt, segmentEndsAt);

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

        var createResult = EventService.Create(EventTier.Small, title, hostParticipantId, EventTestConstants.DefaultBannerUrl);
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
        Guid actingUserId,
        DateTime? segmentStartsAt = null,
        DateTime? segmentEndsAt = null,
        AdmissionType admissionType = AdmissionType.Free)
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
                    StartsAt = segmentStartsAt,
                    EndsAt = segmentEndsAt,
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

    private static GetEventQueryHandler CreateHandler(
        ApplicationDbContext context,
        IUserIdentityAccessor identity)
    {
        var currentUserService = new CurrentUserService(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));

        return new GetEventQueryHandler(
            context,
            identity,
            currentUserService,
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
