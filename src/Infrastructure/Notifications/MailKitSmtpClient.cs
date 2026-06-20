using Application.Notifications;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace Infrastructure.Notifications;

internal sealed class MailKitSmtpClient(IOptions<NotificationsOptions> options) : ISmtpClient
{
    private readonly SmtpClient _client = new();

    public async Task SendAsync(MimeMessage message, CancellationToken cancellationToken)
    {
        var smtp = options.Value.Smtp;

        if (string.IsNullOrWhiteSpace(smtp.Host))
        {
            throw new InvalidOperationException("Notifications:Smtp:Host is not configured.");
        }

        await _client.ConnectAsync(
            smtp.Host,
            smtp.Port,
            smtp.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(smtp.Username))
        {
            await _client.AuthenticateAsync(smtp.Username, smtp.Password, cancellationToken);
        }

        await _client.SendAsync(message, cancellationToken);
        await _client.DisconnectAsync(true, cancellationToken);
    }

    public void Dispose() => _client.Dispose();
}
