using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Media;
using Application.Abstractions.Payments;
using Infrastructure.Authentication;
using Infrastructure.Database;
using Infrastructure.Media;
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
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

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
        services.AddScoped<ITicketTypeRowLock, TicketTypeRowLock>();

        return services;
    }
}
