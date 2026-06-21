using Domain.Events;
using Domain.Groups;
using Domain.Plugins;
using Domain.Search;
using Domain.Subscriptions;
using Domain.Tickets;
using Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Application.Abstractions.Data;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Group> Groups { get; }
    DbSet<GroupMembership> GroupMemberships { get; }
    DbSet<GroupJoinApplication> GroupJoinApplications { get; }
    DbSet<GroupVerificationApplication> GroupVerificationApplications { get; }
    DbSet<Event> Events { get; }
    DbSet<EventOrganizer> EventOrganizers { get; }
    DbSet<EventAttendee> EventAttendees { get; }
    DbSet<EventCategory> EventCategories { get; }
    DbSet<Plugin> Plugins { get; }
    DbSet<PluginUsage> PluginUsages { get; }
    DbSet<TicketType> TicketTypes { get; }
    DbSet<Order> Orders { get; }
    DbSet<Ticket> Tickets { get; }
    DbSet<TicketCode> TicketCodes { get; }
    DbSet<TicketValidation> TicketValidations { get; }
    DbSet<UserSubscription> UserSubscriptions { get; }
    DbSet<EventEmbedding> EventEmbeddings { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
