using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace EventHub.Messaging;

public interface IRabbitMqConnection
{
    Task<IConnection> GetAsync(CancellationToken ct = default);
}

/// <summary>
/// One shared connection per process. Automatic recovery is on, so after a broker restart the client
/// reconnects and re-declares its topology, channels and consumers on its own.
/// </summary>
public sealed class RabbitMqConnection : IRabbitMqConnection, IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;

    public RabbitMqConnection(IOptions<RabbitMqOptions> options) => _options = options.Value;

    public async Task<IConnection> GetAsync(CancellationToken ct = default)
    {
        if (_connection is not null) return _connection;

        await _gate.WaitAsync(ct);
        try
        {
            if (_connection is not null) return _connection;

            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                VirtualHost = _options.VirtualHost,
                UserName = _options.UserName,
                Password = _options.Password,
                ClientProvidedName = _options.ClientName,
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
            };

            // Only cached when the connect succeeds, so a broker that is down at startup is retried later.
            _connection = await factory.CreateConnectionAsync(ct);
            return _connection;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null) await _connection.DisposeAsync();
        _gate.Dispose();
    }
}
