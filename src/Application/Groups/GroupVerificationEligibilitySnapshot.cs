namespace Application.Groups;

public sealed record GroupVerificationEligibilitySnapshot(
    int VerifiedMemberCount,
    int CompletedEventCount,
    int MinVerifiedMembers,
    int MinCompletedEvents,
    bool MeetsEligibility);
