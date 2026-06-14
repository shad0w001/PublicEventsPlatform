using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Groups;
using Application.Groups.Services;
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

        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<GroupAccessService>();

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
