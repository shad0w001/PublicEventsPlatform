using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Media;
using Application.Abstractions.Notifications;
using Application.Abstractions.Payments;
using Infrastructure.Authentication;
using Infrastructure.Database;
using Infrastructure.Media;
using Infrastructure.Messaging;
using Infrastructure.Notifications;
using Infrastructure.Payments;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsqlOptions => npgsqlOptions.UseVector()));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = configuration["Auth0:Domain"];
                options.Audience = configuration["Auth0:Audience"];
                options.MapInboundClaims = false;
            });

        services.AddAuthorization();
        services.AddHttpContextAccessor();
        services.AddScoped<IUserIdentityAccessor, UserIdentityAccessor>();
        services.AddScoped<IMediaStorageService, LocalMediaStorageService>();
        services.AddScoped<ICheckoutSessionProvider, StripeCheckoutSessionProvider>();
        services.AddScoped<IStripeWebhookVerifier, StripeWebhookVerifier>();
        services.AddScoped<ITicketTypeRowLock, TicketTypeRowLock>();
        services.AddScoped<ITicketRowLock, TicketRowLock>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<ISmtpClient, MailKitSmtpClient>();

        services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));
        services.AddSingleton<IKafkaProducer, KafkaProducer>();
        services.AddScoped<OutboxPublishingService>();
        services.AddScoped<ConsumerIdempotencyService>();
        services.AddHostedService<OutboxDispatcher>();
        services.AddHostedService<NotificationKafkaConsumer>();
        services.AddHostedService<CascadeKafkaConsumer>();
        services.AddHostedService<IndexerKafkaConsumer>();

        return services;
    }
}
