using EventHub.Admin.Models;
using EventHub.Contracts;

namespace EventHub.Admin.Services;

public interface IModerationService
{
    Task ReceiveSubmissionAsync(EventSubmitted message, CancellationToken ct = default);
    Task<List<ModerationItem>> ListAsync(ModerationStatus status, CancellationToken ct = default);
    Task<ModerationItem?> GetAsync(Guid id, CancellationToken ct = default);
    Task<Dictionary<int, string>> GetCategoryNamesAsync(CancellationToken ct = default);
    Task ApproveAsync(Guid itemId, Guid moderatorId, bool featured, CancellationToken ct = default);
    Task RejectAsync(Guid itemId, Guid moderatorId, string reasonCode, string reasonText, CancellationToken ct = default);
}
