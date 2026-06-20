namespace Application.Notifications;

public sealed record EmailMessage(
    IReadOnlyList<string> To,
    string Subject,
    string Body);
