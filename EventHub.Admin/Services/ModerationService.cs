using EventHub.Admin.Models;
using EventHub.Admin.Repositories;
using EventHub.Contracts;
using EventHub.Messaging;

namespace EventHub.Admin.Services;

/// <summary>Moderation rules. A decision and its outbox message commit in one transaction.</summary>
public sealed class ModerationService : IModerationService
{
    private readonly IModerationRepository _repo;
    private readonly IOutbox _outbox;
    private readonly ILogger<ModerationService> _logger;

    public ModerationService(IModerationRepository repo, IOutbox outbox, ILogger<ModerationService> logger)
    {
        _repo = repo;
        _outbox = outbox;
        _logger = logger;
    }

    public async Task ReceiveSubmissionAsync(EventSubmitted message, CancellationToken ct = default)
    {
        if (await _repo.ExistsForEventAsync(message.EventId, ct)) return; // already received

        await _repo.AddAsync(ModerationItem.FromSubmission(message), ct);
        await _repo.SaveChangesAsync(ct);
        _logger.LogInformation("Received submission for event {EventId}: {Title}", message.EventId, message.Title);
    }

    public Task<List<ModerationItem>> ListAsync(ModerationStatus status, CancellationToken ct = default) =>
        _repo.ListAsync(status, ct);

    public Task<ModerationItem?> GetAsync(Guid id, CancellationToken ct = default) => _repo.GetAsync(id, ct);

    public Task<Dictionary<int, string>> GetCategoryNamesAsync(CancellationToken ct = default) =>
        _repo.GetCategoryNamesAsync(ct);

    public async Task ApproveAsync(Guid itemId, Guid moderatorId, bool featured, CancellationToken ct = default)
    {
        var item = await _repo.GetAsync(itemId, ct) ?? throw new InvalidOperationException("Submission not found.");

        item.Approve(moderatorId, featured);
        _outbox.Enqueue(new EventApproved(item.EventId, moderatorId, item.DecidedAtUtc!.Value, featured)
        {
            CorrelationId = item.EventId
        });

        await _repo.SaveChangesAsync(ct); // decision + outbox row together
        _logger.LogInformation("Event {EventId} approved by {Moderator}", item.EventId, moderatorId);
    }

    public async Task RejectAsync(Guid itemId, Guid moderatorId, string reasonCode, string reasonText, CancellationToken ct = default)
    {
        var item = await _repo.GetAsync(itemId, ct) ?? throw new InvalidOperationException("Submission not found.");

        item.Reject(moderatorId, reasonText);
        _outbox.Enqueue(new EventRejected(item.EventId, moderatorId, reasonCode, reasonText)
        {
            CorrelationId = item.EventId
        });

        await _repo.SaveChangesAsync(ct);
        _logger.LogInformation("Event {EventId} rejected by {Moderator}: {Reason}", item.EventId, moderatorId, reasonCode);
    }
}
