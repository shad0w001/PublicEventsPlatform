using Application.Abstractions.Data;
using Domain.Users.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Events.Services;

internal static class EventHostDisplayNameLookup
{
    public static async Task<string> ResolveAsync(
        IApplicationDbContext context,
        Guid hostParticipantId,
        bool hostIsGroup,
        CancellationToken cancellationToken)
    {
        if (hostIsGroup)
        {
            var groupName = await context.Groups
                .AsNoTracking()
                .Where(g => g.Id == hostParticipantId)
                .Select(g => g.Name)
                .FirstOrDefaultAsync(cancellationToken);

            return groupName ?? "Organization";
        }

        var user = await context.Users
            .AsNoTracking()
            .Where(u => u.Id == hostParticipantId)
            .Select(u => new { u.Username, u.Email })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return "Host";
        }

        if (!string.IsNullOrWhiteSpace(user.Username))
        {
            return user.Username;
        }

        return UserService.CreateDefaultUsernameFromEmail(user.Email);
    }

    public static async Task<HostDisplayNameLookup> ResolveBatchAsync(
        IApplicationDbContext context,
        IReadOnlyList<Guid> hostParticipantIds,
        CancellationToken cancellationToken)
    {
        if (hostParticipantIds.Count == 0)
        {
            return new HostDisplayNameLookup([], []);
        }

        var distinctHostIds = hostParticipantIds.Distinct().ToList();

        var groups = await context.Groups
            .AsNoTracking()
            .Where(g => distinctHostIds.Contains(g.Id))
            .Select(g => new { g.Id, g.Name })
            .ToListAsync(cancellationToken);

        var groupHostIds = groups.Select(g => g.Id).ToHashSet();
        var names = groups.ToDictionary(g => g.Id, g => g.Name);

        var userHostIds = distinctHostIds.Where(id => !groupHostIds.Contains(id)).ToList();

        if (userHostIds.Count > 0)
        {
            var users = await context.Users
                .AsNoTracking()
                .Where(u => userHostIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Username, u.Email })
                .ToListAsync(cancellationToken);

            foreach (var user in users)
            {
                names[user.Id] = !string.IsNullOrWhiteSpace(user.Username)
                    ? user.Username
                    : UserService.CreateDefaultUsernameFromEmail(user.Email);
            }
        }

        return new HostDisplayNameLookup(groupHostIds, names);
    }

    internal sealed record HostDisplayNameLookup(
        HashSet<Guid> GroupHostIds,
        Dictionary<Guid, string> Names);
}
