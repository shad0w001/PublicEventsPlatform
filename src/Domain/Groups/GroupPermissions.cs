namespace Domain.Groups;

/// <summary>
/// Capability-based group authorization. Organizer and Moderator are peer specializations;
/// Administrator and Owner supersede both.
/// </summary>
public static class GroupPermissions
{
    public static bool CanManageMembers(GroupMemberRole role) =>
        role is GroupMemberRole.Moderator or GroupMemberRole.Administrator or GroupMemberRole.Owner;

    public static bool CanReviewApplications(GroupMemberRole role) =>
        role is GroupMemberRole.Moderator or GroupMemberRole.Administrator or GroupMemberRole.Owner;

    public static bool CanEditProfile(GroupMemberRole role) =>
        role is GroupMemberRole.Administrator or GroupMemberRole.Owner;

    public static bool CanChangeJoinPolicy(GroupMemberRole role) =>
        role is GroupMemberRole.Administrator or GroupMemberRole.Owner;

    public static bool CanCreateEventsAsGroup(GroupMemberRole role) =>
        role is GroupMemberRole.Organizer or GroupMemberRole.Administrator or GroupMemberRole.Owner;

    public static bool CanSoftDeleteGroup(GroupMemberRole role) =>
        role is GroupMemberRole.Owner;

    public static bool CanAssignRole(GroupMemberRole actor, GroupMemberRole targetRole) =>
        actor switch
        {
            GroupMemberRole.Owner => true,
            GroupMemberRole.Administrator => targetRole is not GroupMemberRole.Owner,
            GroupMemberRole.Moderator => targetRole is GroupMemberRole.Member or GroupMemberRole.Organizer,
            _ => false
        };

    public static bool CanRemoveMember(GroupMemberRole actorRole, GroupMemberRole targetRole, bool isSelfLeave)
    {
        if (isSelfLeave)
        {
            return targetRole is not GroupMemberRole.Owner;
        }

        if (targetRole is GroupMemberRole.Owner)
        {
            return false;
        }

        if (!CanManageMembers(actorRole))
        {
            return false;
        }

        return GetRoleRank(actorRole) > GetRoleRank(targetRole);
    }

    public static int GetRoleRank(GroupMemberRole role) => role switch
    {
        GroupMemberRole.Member => 0,
        GroupMemberRole.Organizer => 1,
        GroupMemberRole.Moderator => 2,
        GroupMemberRole.Administrator => 3,
        GroupMemberRole.Owner => 4,
        _ => -1
    };
}
