using Application.Abstractions.Notifications;
using Application.Notifications;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Infrastructure.Notifications;

internal sealed class SmtpEmailSender(
    IOptions<NotificationsOptions> options,
    ISmtpClient smtpClient) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var notificationOptions = options.Value;

        if (string.IsNullOrWhiteSpace(notificationOptions.FromAddress))
        {
            throw new InvalidOperationException("Notifications:FromAddress is not configured.");
        }

        if (string.IsNullOrWhiteSpace(notificationOptions.Smtp.Host))
        {
            throw new InvalidOperationException("Notifications:Smtp:Host is not configured.");
        }

        if (message.To.Count == 0)
        {
            throw new InvalidOperationException("Email message must have at least one recipient.");
        }

        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress(
            notificationOptions.FromDisplayName,
            notificationOptions.FromAddress));

        foreach (var recipient in message.To)
        {
            mimeMessage.To.Add(MailboxAddress.Parse(recipient));
        }

        mimeMessage.Subject = message.Subject;
        mimeMessage.Body = new TextPart("plain") { Text = message.Body };

        await smtpClient.SendAsync(mimeMessage, cancellationToken);
    }
}
