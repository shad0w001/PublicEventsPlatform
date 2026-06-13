using Domain.Groups;

namespace DomainTests.Groups;

public class GroupPermissionsTests
{
    [Fact]
    public void GroupPermissions_Should_DenyManageMembers_When_RoleIsOrganizer()
    {
        // Arrange
        var role = GroupMemberRole.Organizer;

        // Act
        var canManage = GroupPermissions.CanManageMembers(role);

        // Assert
        Assert.False(canManage);
    }

    [Fact]
    public void GroupPermissions_Should_DenyCreateEventsAsGroup_When_RoleIsModerator()
    {
        // Arrange
        var role = GroupMemberRole.Moderator;

        // Act
        var canCreate = GroupPermissions.CanCreateEventsAsGroup(role);

        // Assert
        Assert.False(canCreate);
    }

    [Fact]
    public void GroupPermissions_Should_AllowCreateEventsAsGroup_When_RoleIsOrganizer()
    {
        // Arrange
        var role = GroupMemberRole.Organizer;

        // Act
        var canCreate = GroupPermissions.CanCreateEventsAsGroup(role);

        // Assert
        Assert.True(canCreate);
    }

    [Fact]
    public void GroupPermissions_Should_DenyAssignModerator_When_ActorIsModerator()
    {
        // Arrange
        var actor = GroupMemberRole.Moderator;
        var target = GroupMemberRole.Moderator;

        // Act
        var canAssign = GroupPermissions.CanAssignRole(actor, target);

        // Assert
        Assert.False(canAssign);
    }

    [Fact]
    public void GroupPermissions_Should_AllowAssignOrganizer_When_ActorIsModerator()
    {
        // Arrange
        var actor = GroupMemberRole.Moderator;
        var target = GroupMemberRole.Organizer;

        // Act
        var canAssign = GroupPermissions.CanAssignRole(actor, target);

        // Assert
        Assert.True(canAssign);
    }

    [Fact]
    public void GroupPermissions_Should_DenyAssignOwner_When_ActorIsAdministrator()
    {
        // Arrange
        var actor = GroupMemberRole.Administrator;
        var target = GroupMemberRole.Owner;

        // Act
        var canAssign = GroupPermissions.CanAssignRole(actor, target);

        // Assert
        Assert.False(canAssign);
    }

    [Fact]
    public void GroupPermissions_Should_AllowAssignOwner_When_ActorIsOwner()
    {
        // Arrange
        var actor = GroupMemberRole.Owner;
        var target = GroupMemberRole.Owner;

        // Act
        var canAssign = GroupPermissions.CanAssignRole(actor, target);

        // Assert
        Assert.True(canAssign);
    }

    [Fact]
    public void GroupPermissions_Should_AllowManageMembers_When_RoleIsModerator()
    {
        // Arrange
        var role = GroupMemberRole.Moderator;

        // Act
        var canManage = GroupPermissions.CanManageMembers(role);

        // Assert
        Assert.True(canManage);
    }

    [Fact]
    public void GroupPermissions_Should_AllowSoftDelete_When_RoleIsOwner()
    {
        // Arrange
        var role = GroupMemberRole.Owner;

        // Act
        var canDelete = GroupPermissions.CanSoftDeleteGroup(role);

        // Assert
        Assert.True(canDelete);
    }
}
