using Application.Notifications.Handlers;
using Application.Notifications.Services;
using Domain.Events;
using Domain.Events.EventLocations;
using Domain.Events.Events;
using Domain.Events.Services;
using Domain.Users;
using Domain.Users.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApplicationTests.Notifications.Handlers;

using ApplicationTests.Events;

public class EventRsvpStatusChangedEmailHandlerTests
{
    [Theory]
    [InlineData(EventAttendeeStatus.Going, "Going")]
    [InlineData(EventAttendeeStatus.Interested, "Interested")]
    [InlineData(EventAttendeeStatus.NotGoing, "Not going")]
    public async Task HandleAsync_Should_SendRsvpEmailWithStatus_When_UserRsvps(
        EventAttendeeStatus status,
        string expectedStatusLabel)
    {
        // Arrange
        var databaseName = Guid.NewGuid().ToString();
        var user = CreateUser("auth0|rsvp-user", "rsvp-user@example.com");
        var eventId = await SeedPublishedFreeEventAsync(databaseName, user);
        var fakeEmailSender = new FakeEmailSender();
        var handler = CreateHandler(databaseName, fakeEmailSender);

        // Act
        await handler.HandleAsync(
            new EventRsvpStatusChanged(eventId, user.Id, status, null),
            CancellationToken.None);

        // Assert
        Assert.NotNull(fakeEmailSender.LastMessage);
        Assert.Equal(["rsvp-user@example.com"], fakeEmailSender.LastMessage!.To);
        Assert.Contains("RSVP updated", fakeEmailSender.LastMessage.Subject);
        Assert.Contains(expectedStatusLabel, fakeEmailSender.LastMessage.Body);
        Assert.Contains($"http://localhost:5173/events/{eventId}", fakeEmailSender.LastMessage.Body);
    }

    private static EventRsvpStatusChangedEmailHandler CreateHandler(
        string databaseName,
        FakeEmailSender fakeEmailSender)
    {
        var context = NotificationHandlerTestSupport.CreateContext(databaseName);
        return new EventRsvpStatusChangedEmailHandler(
            context,
            new NotificationRecipientService(context),
            NotificationHandlerTestSupport.CreateLinkBuilder(),
            fakeEmailSender,
            NullLogger<EventRsvpStatusChangedEmailHandler>.Instance);
    }

    private static async Task<Guid> SeedPublishedFreeEventAsync(string databaseName, User hostUser)
    {
        var hostParticipantId = hostUser.Id;
        var createResult = EventService.Create(EventTier.Small, "Community Meetup", hostParticipantId, EventTestConstants.DefaultBannerUrl);
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

        await using var context = NotificationHandlerTestSupport.CreateContext(databaseName);
        context.Users.Add(hostUser);
        context.Events.Add(@event);
        context.EventOrganizers.Add(createResult.Value.Organizer);
        await context.SaveChangesAsync();

        return @event.Id;
    }

    private static User CreateUser(string subject, string email) =>
        UserService.ProvisionFromExternalIdentity(
            subject,
            email,
            emailVerified: true,
            profilePictureUrl: null,
            NotificationHandlerTestSupport.DefaultAvatarUrl,
            ServiceRole.User);
}
