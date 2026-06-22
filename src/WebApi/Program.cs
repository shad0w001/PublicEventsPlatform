using Application;
using Infrastructure;
using Infrastructure.Database;
using Infrastructure.Messaging;
using Infrastructure.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using WebApi;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Services.Configure<HostOptions>(options =>
    {
        options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
    });
}

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.PostConfigure<Application.Media.MediaOptions>(options =>
    options.WebRootPath = builder.Environment.WebRootPath ?? string.Empty);
builder.Services.AddWebApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await WaitForDatabaseAsync(dbContext);
    await dbContext.Database.MigrateAsync();
}

await KafkaHostWait.WaitForBrokerAsync(app.Configuration);

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
        .CreateLogger("Infrastructure.Messaging.KafkaTopicProvisioner");
    await KafkaTopicProvisioner.EnsureTopicsAsync(app.Configuration, logger);
}

using (var scope = app.Services.CreateScope())
{
    await EmbeddingsStartupValidator.ValidateAsync(scope.ServiceProvider);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    app.MapGet("/", () => Results.Redirect("/swagger"))
        .ExcludeFromDescription();
}

app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.UseCors("Spa");
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

static async Task WaitForDatabaseAsync(ApplicationDbContext dbContext, int maxAttempts = 30)
{
    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        if (await dbContext.Database.CanConnectAsync())
        {
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(1));
    }

    throw new InvalidOperationException(
        "Could not connect to PostgreSQL. Ensure the Docker database is running (docker compose up -d).");
}
