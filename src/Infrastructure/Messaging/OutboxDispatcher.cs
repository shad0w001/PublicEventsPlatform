using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Messaging;

internal sealed class OutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> options,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollInterval = TimeSpan.FromSeconds(
            options.Value.OutboxPollIntervalSeconds > 0
                ? options.Value.OutboxPollIntervalSeconds
                : 5);

        logger.LogInformation(
            "Outbox dispatcher started (poll interval {PollIntervalSeconds}s)",
            pollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                var publishingService = scope.ServiceProvider.GetRequiredService<OutboxPublishingService>();
                await publishingService.PublishPendingAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Outbox dispatcher iteration failed");
            }

            try
            {
                await Task.Delay(pollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
