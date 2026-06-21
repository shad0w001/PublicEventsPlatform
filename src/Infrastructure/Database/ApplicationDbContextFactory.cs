using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Database;

public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var configuration = BuildConfiguration();
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is missing or empty. " +
                "Copy src/WebApi/appsettings.Development.example.json to appsettings.Development.json " +
                "and set Password to match POSTGRES_PASSWORD in .env at the solution root.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(
            connectionString,
            npgsqlOptions => npgsqlOptions.UseVector());

        return new ApplicationDbContext(optionsBuilder.Options);
    }

    private static IConfiguration BuildConfiguration()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var webApiPath = ResolveWebApiPath();

        return new ConfigurationBuilder()
            .SetBasePath(webApiPath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();
    }

    private static string ResolveWebApiPath()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "..", "WebApi"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "WebApi"),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "WebApi"),
        };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (File.Exists(Path.Combine(fullPath, "appsettings.json")))
            {
                return fullPath;
            }
        }

        throw new InvalidOperationException(
            "Could not locate WebApi/appsettings.json for EF design-time tools. " +
            "Run dotnet ef from src/Infrastructure with --startup-project ../WebApi.");
    }
}
