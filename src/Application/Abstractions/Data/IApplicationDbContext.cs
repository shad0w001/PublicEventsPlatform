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

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
