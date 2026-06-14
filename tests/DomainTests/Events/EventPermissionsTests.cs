using Domain.Events;
using Domain.Groups;

namespace DomainTests.Events;

public class EventPermissionsTests
{
    private static readonly Guid HostUserParticipantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CreatorUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid GroupHostParticipantId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public void EventPermissions_Should_AllowEdit_When_UserIsHostParticipant()
    {
        // Arrange
        var @event = new Event { CreatedByUserId = null };

        // Act
        var canEdit = EventPermissions.CanEdit(
            @event,
            actorUserId: HostUserParticipantId,
            actorParticipantId: HostUserParticipantId,
            hostParticipantId: HostUserParticipantId,
            hostIsGroup: false,
            groupRole: null);

        // Assert
        Assert.True(canEdit);
    }

    [Fact]
    public void EventPermissions_Should_AllowEdit_When_UserIsCreatedByUserId()
    {
        // Arrange
        var @event = new Event { CreatedByUserId = CreatorUserId };

        // Act
        var canEdit = EventPermissions.CanEdit(
            @event,
            actorUserId: CreatorUserId,
            actorParticipantId: OtherUserId,
            hostParticipantId: GroupHostParticipantId,
            hostIsGroup: true,
            groupRole: GroupMemberRole.Member);

        // Assert
        Assert.True(canEdit);
    }

    [Fact]
    public void EventPermissions_Should_AllowEdit_When_GroupOrganizer()
    {
        // Arrange
        var @event = new Event { CreatedByUserId = null };

        // Act
        var canEdit = EventPermissions.CanEdit(
            @event,
            actorUserId: OtherUserId,
            actorParticipantId: OtherUserId,
            hostParticipantId: GroupHostParticipantId,
            hostIsGroup: true,
            groupRole: GroupMemberRole.Organizer);

        // Assert
        Assert.True(canEdit);
    }

    [Fact]
    public void EventPermissions_Should_DenyEdit_When_GroupMemberWithoutRole()
    {
        // Arrange
        var @event = new Event { CreatedByUserId = null };

        // Act
        var canEdit = EventPermissions.CanEdit(
            @event,
            actorUserId: OtherUserId,
            actorParticipantId: OtherUserId,
            hostParticipantId: GroupHostParticipantId,
            hostIsGroup: true,
            groupRole: GroupMemberRole.Member);

        // Assert
        Assert.False(canEdit);
    }

    [Fact]
    public void EventPermissions_Should_AllowCancel_When_PublishedAndEditor()
    {
        // Arrange
        var @event = new Event { Status = EventStatus.Published, CreatedByUserId = CreatorUserId };

        // Act
        var canCancel = EventPermissions.CanCancel(
            @event,
            CreatorUserId,
            HostUserParticipantId,
            HostUserParticipantId,
            hostIsGroup: false,
            groupRole: null);

        // Assert
        Assert.True(canCancel);
    }

    [Fact]
    public void EventPermissions_Should_DenySoftDelete_When_NotDraft()
    {
        // Arrange
        var @event = new Event { Status = EventStatus.Published, CreatedByUserId = CreatorUserId };

        // Act
        var canDelete = EventPermissions.CanSoftDelete(
            @event,
            CreatorUserId,
            HostUserParticipantId,
            HostUserParticipantId,
            hostIsGroup: false,
            groupRole: null);

        // Assert
        Assert.False(canDelete);
    }
}
