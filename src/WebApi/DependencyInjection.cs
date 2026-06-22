using System.Text.Json.Serialization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Annotations;

namespace WebApi;

public static class DependencyInjection
{
    public static IServiceCollection AddWebApi(this IServiceCollection services)
    {
        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Public Events Platform API",
                Version = "v1",
                Description =
                    "Thesis backend for public events from small gatherings to conferences. " +
                    "Auth0 JWT; groups, events, tickets, discovery, and verified organizations."
            });

            options.EnableAnnotations();

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "JWT Authorization header using the Bearer scheme."
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            });
        });

        services.AddCors(options =>
        {
            options.AddPolicy("Spa", policy =>
            {
                policy.WithOrigins("http://localhost:5173")
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        return services;
    }
}
