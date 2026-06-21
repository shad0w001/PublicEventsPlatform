using Application.Abstractions.Authentication;
using Application.Groups.DecideGroupVerificationApplication;
using Application.Groups.Services;
using Application.Groups.SubmitGroupVerificationApplication;
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

namespace ApplicationTests.Groups;

internal static class GroupVerificationTestData
{
    public const string DefaultAvatarUrl = "/images/default-avatar.png";
    public const string DefaultGroupImageUrl = "/images/default-group.png";

    public static User CreateUser(
        string subject,
        string email,
        bool emailVerified = true,
        ServiceRole role = ServiceRole.User) =>
        UserService.ProvisionFromExternalIdentity(
            subject,
            email,
            emailVerified,
            profilePictureUrl: null,
            DefaultAvatarUrl,
            role);

    public static async Task<(Group Group, User Owner)> SeedEligibleGroupAsync(string databaseName)
    {
        var owner = CreateUser("auth0|verify-owner", "verify-owner@example.com");
        var members = Enumerable.Range(1, 4)
            .Select(i => CreateUser($"auth0|verify-member-{i}", $"member{i}@example.com"))
            .ToList();

        var createResult = GroupService.Create(
            "Verify Candidate Org",
            "Org seeking verification badge",
            GroupJoinPolicy.Open,
            owner.Id,
            DefaultGroupImageUrl);
        var group = createResult.Value.Group;

        await using (var seedContext = CreateContext(databaseName))
        {
            seedContext.Users.Add(owner);
            seedContext.Users.AddRange(members);
            seedContext.Groups.Add(group);
            seedContext.GroupMemberships.Add(createResult.Value.OwnerMembership);

            foreach (var member in members)
            {
                seedContext.GroupMemberships.Add(
                    GroupMembership.Create(group.Id, member.Id, GroupMemberRole.Member));
            }

            var category = new EventCategory { Name = "Verification Test" };
            seedContext.EventCategories.Add(category);
            await seedContext.SaveChangesAsync();

            var utcNow = DateTime.UtcNow;
            for (var i = 0; i < GroupVerificationConstants.MinCompletedEvents; i++)
            {
                var eventCreate = EventService.Create(
                    EventTier.Small,
                    $"Completed Event {i + 1}",
                    group.Id);
                var @event = eventCreate.Value.Event;
                var organizer = eventCreate.Value.Organizer;

                var patch = new EventUpdatePatch
                {
                    Description = "Completed verification test event",
                    CategoryId = category.Id,
                    StartTime = utcNow.AddDays(-30 + i),
                    EndTime = utcNow.AddDays(-29 + i),
                    TimeZoneId = "Europe/Sofia",
                    AdmissionType = AdmissionType.Free,
                    Locations =
                    [
                        new EventLocation
                        {
                            Name = "Hall",
                            Kind = EventLocationKind.Physical,
                            Address = "1 Test St",
                            City = "Sofia"
                        }
                    ]
                };

                EventService.Update(@event, patch, owner.Id);
                EventService.Publish(@event, categoryExists: true, recentPublishCount: 0, maxPublishesPerWeek: 100);

                seedContext.Events.Add(@event);
                seedContext.EventOrganizers.Add(organizer);
            }

            await seedContext.SaveChangesAsync();
        }

        return (group, owner);
    }

    public static ApplicationDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new ApplicationDbContext(options);
    }

    public static CurrentUserService CreateCurrentUserService(
        ApplicationDbContext context,
        IUserIdentityAccessor identity) =>
        new(
            context,
            identity,
            Options.Create(new UserProfileOptions { DefaultAvatarUrl = DefaultAvatarUrl }));

    public static FakeUserIdentityAccessor CreateVerifiedIdentity(
        User user,
        ServiceRole role = ServiceRole.User) =>
        new()
        {
            IsAuthenticated = true,
            ExternalSubjectId = user.ExternalSubjectId,
            Email = user.Email,
            EmailVerified = user.EmailVerified,
            ServiceRole = role
        };

    public sealed class FakeUserIdentityAccessor : IUserIdentityAccessor
    {
        public bool IsAuthenticated { get; init; }
        public string ExternalSubjectId { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public bool EmailVerified { get; set; }
        public ServiceRole ServiceRole { get; init; }
        public string? ProfilePictureUrl { get; init; }
    }
}
