using EventHub.Web.Models;
using EventHub.Web.Repositories;
using EventHub.Web.ViewModels;

namespace EventHub.Web.Services;

public interface IContactService
{
    Task<Guid> SubmitAsync(ContactFormViewModel vm, Guid? userId, CancellationToken ct = default);
    Task<List<ContactMessage>> ListAsync(bool? open, CancellationToken ct = default);
    Task<int> CountOpenAsync(CancellationToken ct = default);
    /// <summary>Marks a message handled (or open again). Returns false when it does not exist.</summary>
    Task<bool> SetHandledAsync(Guid id, bool handled, string adminName, CancellationToken ct = default);
}

public sealed class ContactService : IContactService
{
    private readonly IContactRepository _repo;
    private readonly ILogger<ContactService> _logger;

    public ContactService(IContactRepository repo, ILogger<ContactService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task<Guid> SubmitAsync(ContactFormViewModel vm, Guid? userId, CancellationToken ct = default)
    {
        var message = new ContactMessage
        {
            Id = Guid.NewGuid(),
            Name = vm.Name.Trim(),
            Email = vm.Email.Trim(),
            Topic = ContactTopics.All.Contains(vm.Topic) ? vm.Topic : ContactTopics.Other,
            Message = vm.Message.Trim(),
            UserId = userId
        };
        await _repo.AddAsync(message, ct);
        await _repo.SaveChangesAsync(ct);
        _logger.LogInformation("Contact message {MessageId} received ({Topic})", message.Id, message.Topic);
        return message.Id;
    }

    public Task<List<ContactMessage>> ListAsync(bool? open, CancellationToken ct = default) => _repo.ListAsync(open, ct);

    public Task<int> CountOpenAsync(CancellationToken ct = default) => _repo.CountOpenAsync(ct);

    public async Task<bool> SetHandledAsync(Guid id, bool handled, string adminName, CancellationToken ct = default)
    {
        var message = await _repo.GetAsync(id, ct);
        if (message is null) return false;
        if (handled) message.MarkHandled(adminName); else message.Reopen();
        await _repo.SaveChangesAsync(ct);
        return true;
    }
}
