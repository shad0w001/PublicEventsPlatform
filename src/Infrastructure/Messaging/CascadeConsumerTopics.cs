namespace Infrastructure.Messaging;

internal static class CascadeConsumerNames
{
    public const string GroupSoftDeleted = "cascade.group-soft-deleted";
}

internal static class CascadeConsumerTopics
{
    public const string GroupSoftDeleted = "domain.group-soft-deleted";

    public static readonly string[] CascadeTopics =
    [
        GroupSoftDeleted
    ];
}
