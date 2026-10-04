using Microsoft.EntityFrameworkCore;
using SyWater.Consumption.Api.Messaging;
using SyWater.Consumption.Api.Security;
using SyWater.Consumption.Application.Ports.In;
using SyWater.Consumption.Application.Ports.Out;
using SyWater.Consumption.Application.UseCases;
using SyWater.Consumption.Infrastructure.Persistence;
using SyWater.Consumption.Infrastructure.Places;

namespace SyWater.Consumption.Api.Composition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddConsumptionApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IRecordReadingUseCase, RecordReadingUseCase>();
        services.AddScoped<IGetConsumptionUseCase, GetConsumptionUseCase>();
        return services;
    }

    public static IServiceCollection AddConsumptionInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Consumption");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Missing connection string 'ConnectionStrings:Consumption' (user-secrets).");

        services.AddDbContext<ConsumptionDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IReadingStore, EfReadingStore>();
        services.AddScoped<IConsumptionReader, EfConsumptionReader>();

        var placesUrl = config["Services:PlacesBaseUrl"];
        if (string.IsNullOrWhiteSpace(placesUrl))
            throw new InvalidOperationException("Missing 'Services:PlacesBaseUrl'.");

        services.AddHttpContextAccessor();
        services.AddScoped<IAccessTokenProvider, HttpContextAccessTokenProvider>();
        services.AddHttpClient<IPlaceAccessChecker, HttpPlaceAccessChecker>(client =>
        {
            client.BaseAddress = new Uri(placesUrl.EndsWith('/') ? placesUrl : placesUrl + "/");
            client.Timeout = TimeSpan.FromSeconds(5);
        });
        return services;
    }

    public static IServiceCollection AddConsumptionMessaging(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<RabbitMqOptions>(config.GetSection(RabbitMqOptions.Section));
        if (!string.IsNullOrWhiteSpace(config[$"{RabbitMqOptions.Section}:Host"]))
            services.AddHostedService<ReadingEventsConsumer>();
        return services;
    }
}
