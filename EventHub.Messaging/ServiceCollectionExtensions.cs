using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace EventHub.Messaging;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the RabbitMQ connection, topology, publisher and default (no-op) message log.</summary>
    public static IServiceCollection AddRabbitMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.AddSingleton<IRabbitMqConnection, RabbitMqConnection>();
        services.AddSingleton<TopologyDeclarer>();
        services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();
        services.TryAddScoped<IMessageLog, NullMessageLog>();
        return services;
    }

    /// <summary>Registers a consumer as a hosted background service.</summary>
    public static IServiceCollection AddConsumer<TConsumer>(this IServiceCollection services)
        where TConsumer : class, IHostedService
        => services.AddHostedService<TConsumer>();
}
