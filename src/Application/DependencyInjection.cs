using Application.Abstractions.Authentication;
using Application.Abstractions.Groups;
using Application.Abstractions.Messaging;
using Application.Abstractions.Notifications;
using Application.Events;
using Application.Events.Services;
using Application.Groups;
using Application.Groups.ProcessGroupSoftDeleted;
using Application.Groups.Services;
using Application.Media;
using Application.Notifications;
using Application.Notifications.Handlers;
using Application.Notifications.Services;
using Application.Payments;
using Application.Users;
using Application.Users.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<UserProfileOptions>(
            configuration.GetSection(UserProfileOptions.SectionName));

        services.Configure<GroupProfileOptions>(
            configuration.GetSection(GroupProfileOptions.SectionName));

        services.Configure<EventOptions>(
            configuration.GetSection(EventOptions.SectionName));

        services.Configure<MediaOptions>(
            configuration.GetSection(MediaOptions.SectionName));

        services.Configure<StripeOptions>(
            configuration.GetSection(StripeOptions.SectionName));

        services.Configure<NotificationsOptions>(
            configuration.GetSection(NotificationsOptions.SectionName));

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<NotificationRecipientService>();
        services.AddScoped<NotificationLinkBuilder>();
        services.AddScoped<ITicketPurchaseCompletedEmailHandler, TicketPurchaseCompletedEmailHandler>();
        services.AddScoped<IEventRsvpStatusChangedEmailHandler, EventRsvpStatusChangedEmailHandler>();
        services.AddScoped<IGroupJoinApplicationSubmittedEmailHandler, GroupJoinApplicationSubmittedEmailHandler>();
        services.AddScoped<IGroupJoinApplicationApprovedEmailHandler, GroupJoinApplicationApprovedEmailHandler>();
        services.AddScoped<IGroupJoinApplicationRejectedEmailHandler, GroupJoinApplicationRejectedEmailHandler>();
        services.AddScoped<IEventCancelledEmailHandler, EventCancelledEmailHandler>();
        services.AddScoped<IGroupSoftDeletedCascadeHandler, GroupSoftDeletedCascadeHandler>();
        services.AddScoped<GroupAccessService>();
        services.AddScoped<EventAccessService>();
        services.AddScoped<EventVenueConflictService>();

        services.Scan(scan => scan
            .FromAssembliesOf(typeof(DependencyInjection))
            .AddClasses(classes => classes.AssignableTo(typeof(IQueryHandler<,>)), publicOnly: false)
                .AsImplementedInterfaces()
                .WithScopedLifetime()
            .AddClasses(classes => classes.AssignableTo(typeof(ICommandHandler<>)), publicOnly: false)
                .AsImplementedInterfaces()
                .WithScopedLifetime()
            .AddClasses(classes => classes.AssignableTo(typeof(ICommandHandler<,>)), publicOnly: false)
                .AsImplementedInterfaces()
                .WithScopedLifetime());

        return services;
    }
}
