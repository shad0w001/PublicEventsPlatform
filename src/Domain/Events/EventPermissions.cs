using Domain.Groups;

namespace Domain.Events;

/// <summary>
/// Capability-based event authorization. User-hosted events grant the host participant
/// and CreatedByUserId (set on first PATCH). Group-hosted events grant Organizer+ and CreatedByUserId.
/// </summary>
public static class EventPermissions
{
    public static bool CanEdit(
        Event @event,
        Guid actorUserId,
        Guid actorParticipantId,
        Guid hostParticipantId,
        bool hostIsGroup,
        GroupMemberRole? groupRole) =>
        CanViewDraft(@event, actorUserId, actorParticipantId, hostParticipantId, hostIsGroup, groupRole);

    public static bool CanViewDraft(
        Event @event,
        Guid actorUserId,
        Guid actorParticipantId,
        Guid hostParticipantId,
        bool hostIsGroup,
        GroupMemberRole? groupRole)
    {
        if (@event.CreatedByUserId == actorUserId)
        {
            return true;
        }

        if (hostIsGroup)
        {
            return groupRole is not null &&
                   GroupPermissions.CanCreateEventsAsGroup(groupRole.Value);
        }

        return actorParticipantId == hostParticipantId;
    }

    public static bool CanPublish(
        Event @event,
        Guid actorUserId,
        Guid actorParticipantId,
        Guid hostParticipantId,
        bool hostIsGroup,
        GroupMemberRole? groupRole) =>
        CanEdit(@event, actorUserId, actorParticipantId, hostParticipantId, hostIsGroup, groupRole);

    public static bool CanCancel(
        Event @event,
        Guid actorUserId,
        Guid actorParticipantId,
        Guid hostParticipantId,
        bool hostIsGroup,
        GroupMemberRole? groupRole) =>
        @event.Status == EventStatus.Published &&
        CanEdit(@event, actorUserId, actorParticipantId, hostParticipantId, hostIsGroup, groupRole);

    public static bool CanSoftDelete(
        Event @event,
        Guid actorUserId,
        Guid actorParticipantId,
        Guid hostParticipantId,
        bool hostIsGroup,
        GroupMemberRole? groupRole) =>
        @event.Status == EventStatus.Draft &&
        CanEdit(@event, actorUserId, actorParticipantId, hostParticipantId, hostIsGroup, groupRole);
}
