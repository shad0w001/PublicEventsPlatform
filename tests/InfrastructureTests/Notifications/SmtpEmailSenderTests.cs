using Application.Notifications;
using Infrastructure.Notifications;
using Microsoft.Extensions.Options;
using MimeKit;

namespace InfrastructureTests.Notifications;

public class SmtpEmailSenderTests
{
    [Fact]
    public async Task SendAsync_Should_CaptureRecipientsAndContent_When_MessageIsValid()
    {
        // Arrange
        var fakeClient = new FakeSmtpClient();
        var sender = CreateSender(fakeClient, CreateOptions("noreply@example.com", "smtp.example.com"));
        var message = new EmailMessage(
            ["buyer@example.com", "owner@example.com"],
            "Ticket purchase confirmed",
            "Your tickets are ready.");

        // Act
        await sender.SendAsync(message, CancellationToken.None);

        // Assert
        Assert.NotNull(fakeClient.LastMessage);
        Assert.Equal("Ticket purchase confirmed", fakeClient.LastMessage!.Subject);
        Assert.Equal("Your tickets are ready.", ((TextPart)fakeClient.LastMessage.Body).Text);
        Assert.Equal(2, fakeClient.LastMessage.To.Count);
        Assert.Equal("buyer@example.com", fakeClient.LastMessage.To[0].ToString());
        Assert.Equal("owner@example.com", fakeClient.LastMessage.To[1].ToString());
        Assert.Equal("noreply@example.com", ((MailboxAddress)fakeClient.LastMessage.From[0]).Address);
    }

    [Fact]
    public async Task SendAsync_Should_ThrowInvalidOperationException_When_FromAddressIsMissing()
    {
        // Arrange
        var sender = CreateSender(new FakeSmtpClient(), CreateOptions("", "smtp.example.com"));
        var message = new EmailMessage(["buyer@example.com"], "Subject", "Body");

        // Act
        var act = () => sender.SendAsync(message, CancellationToken.None);

        // Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(act);
        Assert.Contains("FromAddress", exception.Message);
    }

    [Fact]
    public async Task SendAsync_Should_ThrowInvalidOperationException_When_SmtpHostIsMissing()
    {
        // Arrange
        var fakeClient = new FakeSmtpClient();
        var sender = CreateSender(fakeClient, CreateOptions("noreply@example.com", ""));
        var message = new EmailMessage(["buyer@example.com"], "Subject", "Body");

        // Act
        var act = () => sender.SendAsync(message, CancellationToken.None);

        // Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(act);
        Assert.Contains("Host", exception.Message);
        Assert.Null(fakeClient.LastMessage);
    }

    private static SmtpEmailSender CreateSender(FakeSmtpClient fakeClient, NotificationsOptions options) =>
        new(Options.Create(options), fakeClient);

    private static NotificationsOptions CreateOptions(string fromAddress, string smtpHost) =>
        new()
        {
            FromAddress = fromAddress,
            FromDisplayName = "Public Events Platform",
            Smtp = new SmtpOptions
            {
                Host = smtpHost,
                Port = 587,
                EnableSsl = true
            }
        };

    private sealed class FakeSmtpClient : ISmtpClient
    {
        public MimeMessage? LastMessage { get; private set; }

        public Task SendAsync(MimeMessage message, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(message.From.ToString()))
            {
                throw new InvalidOperationException("Notifications:FromAddress is not configured.");
            }

            LastMessage = message;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }
}
