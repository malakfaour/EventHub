namespace EventHub.Web.Models;

/// <summary>What a contact message is about. Shown as the topic list on the Contact page.</summary>
public static class ContactTopics
{
    public const string Tickets = "Tickets I bought";
    public const string Organizing = "Organizing an event";
    public const string Suppliers = "Selling as a supplier";
    public const string Account = "My account";
    public const string Other = "Something else";

    public static readonly IReadOnlyList<string> All = [Tickets, Organizing, Suppliers, Account, Other];
}

/// <summary>A message sent from the public Contact page. Admins read and close them in the Inbox.</summary>
public class ContactMessage
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Topic { get; set; } = ContactTopics.Other;
    public string Message { get; set; } = "";
    /// <summary>Set when the sender was signed in, so the admin can see which account wrote.</summary>
    public Guid? UserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? HandledAtUtc { get; private set; }
    public string? HandledBy { get; private set; }

    public bool IsOpen => HandledAtUtc is null;

    public void MarkHandled(string by)
    {
        if (!IsOpen) return;
        HandledAtUtc = DateTime.UtcNow;
        HandledBy = by;
    }

    public void Reopen()
    {
        HandledAtUtc = null;
        HandledBy = null;
    }
}
