namespace Application.Notifications;

public sealed class NotificationsOptions
{
    public const string SectionName = "Notifications";

    public string PublicAppBaseUrl { get; init; } = "http://localhost:5173";

    public string FromAddress { get; init; } = string.Empty;

    public string FromDisplayName { get; init; } = "Public Events Platform";

    public SmtpOptions Smtp { get; init; } = new();
}
