using Domain.Events;
using Domain.Groups;
using Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Application.Abstractions.Data;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Group> Groups { get; }
    DbSet<GroupMembership> GroupMemberships { get; }
    DbSet<GroupJoinApplication> GroupJoinApplications { get; }
    DbSet<Event> Events { get; }
    DbSet<EventOrganizer> EventOrganizers { get; }
    DbSet<EventCategory> EventCategories { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
