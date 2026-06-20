using MimeKit;

namespace Infrastructure.Notifications;

internal interface ISmtpClient : IDisposable
{
    Task SendAsync(MimeMessage message, CancellationToken cancellationToken);
}
