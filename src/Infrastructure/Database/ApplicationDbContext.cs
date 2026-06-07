using Application.Abstractions.Data;
using Domain.Events;
using Domain.Groups;
using Domain.Participants;
using Domain.Plugins;
using Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Database;

public sealed class ApplicationDbContext : DbContext, IApplicationDbContext
{
        public DbSet<Participant> Participants => Set<Participant>();
        public DbSet<User> Users => Set<User>();
        public DbSet<Group> Groups => Set<Group>();
        public DbSet<GroupMembership> GroupMemberships => Set<GroupMembership>();
        public DbSet<Event> Events => Set<Event>();
        public DbSet<EventOrganizer> EventOrganizers => Set<EventOrganizer>();
        public DbSet<EventAttendee> EventAttendees => Set<EventAttendee>();
        public DbSet<Plugin> Plugins => Set<Plugin>();
        public DbSet<PluginUsage> PluginUsages => Set<PluginUsage>();
        public DbSet<EventCategory> EventCategories => Set<EventCategory>();

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
