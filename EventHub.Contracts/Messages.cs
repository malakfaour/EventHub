namespace EventHub.Contracts;

// ───────────── Client App  ->  Admin App  (exchange: eventhub.client) ─────────────

/// <summary>An organizer submitted a new event for moderation.</summary>
public sealed record EventSubmitted(
    Guid EventId, Guid OrganizerId, string Title, string Description,
    int CategoryId, DateTime StartsAtUtc, string Location,
    int Capacity, decimal TicketPrice) : MessageBase;

/// <summary>An organizer edited a published event; it must be re-reviewed.</summary>
public sealed record EventUpdated(
    Guid EventId, Guid OrganizerId, string Title, string Description,
    int CategoryId, DateTime StartsAtUtc, string Location,
    int Capacity, decimal TicketPrice) : MessageBase;

public sealed record EventCancelled(Guid EventId, Guid OrganizerId, string Reason) : MessageBase;

/// <param name="TargetType">"Event" or "Comment".</param>
public sealed record EventReported(
    Guid ReportId, string TargetType, Guid TargetId, Guid ReporterId, string Reason) : MessageBase;

public sealed record RsvpCreated(Guid RsvpId, Guid EventId, Guid UserId, RsvpStatus Status) : MessageBase;

public sealed record RsvpCancelled(Guid RsvpId, Guid EventId, Guid UserId) : MessageBase;

/// <param name="Rating">Optional 1-5 star rating.</param>
public sealed record CommentPosted(
    Guid CommentId, Guid EventId, Guid UserId, string Text, int? Rating) : MessageBase;

public sealed record UserRegistered(Guid UserId, string Email, string DisplayName) : MessageBase;

/// <summary>Client -> Admin. Carries everything the revenue reports need.</summary>
public sealed record PaymentCompleted(
    Guid PaymentId, Guid UserId, Guid? EventId, PaymentKind Kind,
    decimal Amount, string Currency, string ProviderReference) : MessageBase;

// ───────────── Admin App  ->  Client App  (exchange: eventhub.admin) ─────────────

public sealed record EventApproved(
    Guid EventId, Guid ModeratorId, DateTime ApprovedAtUtc, bool IsFeatured) : MessageBase;

public sealed record EventRejected(
    Guid EventId, Guid ModeratorId, string ReasonCode, string ReasonText) : MessageBase;

public sealed record ReportResolved(
    Guid ReportId, string TargetType, Guid TargetId, Guid ReporterId,
    string Resolution, bool ContentRemoved) : MessageBase;

/// <param name="ExpiresAtUtc">Null means a permanent ban.</param>
public sealed record UserBanned(Guid UserId, string Reason, DateTime? ExpiresAtUtc) : MessageBase;

public sealed record UserUnbanned(Guid UserId) : MessageBase;

public sealed record CategoryChanged(int CategoryId, string Name, bool IsActive) : MessageBase;
