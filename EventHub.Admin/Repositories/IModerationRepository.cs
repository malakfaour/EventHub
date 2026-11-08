using EventHub.Admin.Models;

namespace EventHub.Admin.Repositories;

public interface IModerationRepository
{
    Task AddAsync(ModerationItem item, CancellationToken ct = default);
    Task<ModerationItem?> GetAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsForEventAsync(Guid eventId, CancellationToken ct = default);
    Task<List<ModerationItem>> ListAsync(ModerationStatus status, CancellationToken ct = default);
    Task<Dictionary<int, string>> GetCategoryNamesAsync(CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
