using Application.Abstractions.Notifications;
using Application.Notifications;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApplicationTests.Notifications.Handlers;

internal sealed class FakeEmailSender : IEmailSender
{
    private readonly List<EmailMessage> _messages = [];

    public EmailMessage? LastMessage => _messages.Count > 0 ? _messages[^1] : null;

    public IReadOnlyList<EmailMessage> Messages => _messages;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _messages.Add(message);
        return Task.CompletedTask;
    }
}

internal static class NotificationHandlerTestSupport
{
    internal const string DefaultAvatarUrl = "/images/default-avatar.png";

    internal static NotificationLinkBuilder CreateLinkBuilder() =>
        new(Options.Create(new NotificationsOptions
        {
            PublicAppBaseUrl = "http://localhost:5173"
        }));

    internal static ApplicationDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new ApplicationDbContext(options);
    }
}
