using EventHub.Client.Models;

namespace EventHub.Client.Repositories;

public interface IEventRepository
{
    Task AddAsync(Event ev, CancellationToken ct = default);
    Task<Event?> GetAsync(Guid id, CancellationToken ct = default);
    Task<List<Event>> GetByOrganizerAsync(Guid organizerId, CancellationToken ct = default);
    Task<List<Event>> GetPublishedAsync(CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface ICategoryRepository
{
    Task<List<Category>> GetActiveAsync(CancellationToken ct = default);
}
