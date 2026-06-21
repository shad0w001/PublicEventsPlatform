using Application.Abstractions.Data;
using Domain.Events;
using Domain.Groups;
using Domain.Participants;
using Domain.Plugins;
using Domain.Search;
using Domain.Subscriptions;
using Domain.Tickets;
using Domain.Users;
using Infrastructure.DomainEvents;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Infrastructure.Database;

public sealed class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private readonly DbContextOptions<ApplicationDbContext> _contextOptions;

        public DbSet<Participant> Participants => Set<Participant>();
        public DbSet<User> Users => Set<User>();
        public DbSet<Group> Groups => Set<Group>();
        public DbSet<GroupMembership> GroupMemberships => Set<GroupMembership>();
        public DbSet<GroupJoinApplication> GroupJoinApplications => Set<GroupJoinApplication>();
        public DbSet<Event> Events => Set<Event>();
        public DbSet<EventOrganizer> EventOrganizers => Set<EventOrganizer>();
        public DbSet<EventAttendee> EventAttendees => Set<EventAttendee>();
        public DbSet<Plugin> Plugins => Set<Plugin>();
        public DbSet<PluginUsage> PluginUsages => Set<PluginUsage>();
        public DbSet<EventCategory> EventCategories => Set<EventCategory>();
        public DbSet<TicketType> TicketTypes => Set<TicketType>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<Ticket> Tickets => Set<Ticket>();
        public DbSet<TicketCode> TicketCodes => Set<TicketCode>();
        public DbSet<TicketValidation> TicketValidations => Set<TicketValidation>();
        public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
        public DbSet<EventEmbedding> EventEmbeddings => Set<EventEmbedding>();
        internal DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
        internal DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
        _contextOptions = options;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        if (IsUsingInMemoryProvider())
        {
            modelBuilder.Ignore<EventEmbedding>();
        }

        base.OnModelCreating(modelBuilder);
    }

    private bool IsUsingInMemoryProvider() =>
        _contextOptions.Extensions.Any(e =>
            e.GetType().Name.Contains("InMemory", StringComparison.Ordinal));

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Outbox rows are part of the same transaction as business data (template option 1).
        AddOutboxMessages();

        return await base.SaveChangesAsync(cancellationToken);
    }

    private void AddOutboxMessages()
    {
        var entitiesWithEvents = ChangeTracker
            .Entries<Entity>()
            .Select(entry => entry.Entity)
            .Where(entity => entity.DomainEvents.Count > 0)
            .ToList();

        foreach (var entity in entitiesWithEvents)
        {
            foreach (var domainEvent in entity.DomainEvents.ToList())
            {
                var outboxMessage = OutboxMessageFactory.CreateFromDomainEvent(domainEvent);
                if (outboxMessage is not null)
                {
                    OutboxMessages.Add(outboxMessage);
                }
            }

            entity.ClearDomainEvents();
        }
    }
}
