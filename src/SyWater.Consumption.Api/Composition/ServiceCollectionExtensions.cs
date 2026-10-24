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
        services.AddScoped<IGetGuestConsumptionUseCase, GetGuestConsumptionUseCase>();
        services.AddScoped<IGetGoalProgressUseCase, GetGoalProgressUseCase>();
        services.AddScoped<ISetMonthlyGoalUseCase, SetMonthlyGoalUseCase>();
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
        services.AddScoped<IGoalStore, EfGoalStore>();

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

        // Guest links (HU-040): ms-places says which place a token opens, authenticated with the key shared by the services.
        // Without the key (a local run) no guest link opens anything: the endpoint answers 404 and the rest keeps working.
        var internalKey = config["Internal:ApiKey"];
        if (string.IsNullOrWhiteSpace(internalKey) || internalKey.Length < 24)
        {
            services.AddSingleton<IGuestLinkResolver, GuestLinksNotConfiguredResolver>();
        }
        else
        {
            services.AddHttpClient<IGuestLinkResolver, HttpGuestLinkResolver>((http, _) =>
            {
                http.BaseAddress = new Uri(placesUrl.EndsWith('/') ? placesUrl : placesUrl + "/");
                http.Timeout = TimeSpan.FromSeconds(5);
                return new HttpGuestLinkResolver(http, internalKey);
            });
        }
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
