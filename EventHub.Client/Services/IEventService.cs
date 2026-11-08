using EventHub.Client.Models;
using EventHub.Client.ViewModels;
using EventHub.Contracts;

namespace EventHub.Client.Services;

public interface IEventService
{
    Task<Guid> CreateAndSubmitAsync(CreateEventViewModel vm, Guid organizerId, CancellationToken ct = default);
    Task<List<Event>> GetMyEventsAsync(Guid organizerId, CancellationToken ct = default);
    Task<List<Event>> GetPublishedAsync(CancellationToken ct = default);
    Task<List<Category>> GetCategoriesAsync(CancellationToken ct = default);

    // Called by the RabbitMQ consumer when the Admin App decides.
    Task ApplyApprovedAsync(EventApproved message, CancellationToken ct = default);
    Task ApplyRejectedAsync(EventRejected message, CancellationToken ct = default);
}
