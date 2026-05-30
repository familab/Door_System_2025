using DoorApp.Familab.Application.Abstractions;
using DoorApp.Familab.Application.Options;
using DoorApp.Familab.Application.Services;
using DoorApp.Familab.Domain;
using DoorApp.Familab.Infrastructure.Hardware;
using DoorApp.Familab.Infrastructure.Storage;
using DoorApp.Familab.Infrastructure.Versioning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DoorApp.Familab.Infrastructure;

/// <summary>Wires up domain, application and infrastructure services into the DI container.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddDoorApp(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DoorOptions>()
            .Bind(configuration.GetSection(DoorOptions.SectionName))
            .ValidateOnStart();

        // Core singletons
        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddSingleton<IVersionProvider, AssemblyVersionProvider>();
        services.AddSingleton<IRuntimeStatus, RuntimeStatus>();

        // Storage
        services.AddSingleton<SqliteConnectionFactory>();
        services.AddSingleton<IAccessLogStore, SqliteAccessLogStore>();
        services.AddSingleton<IBadgeStore>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<DoorOptions>>().Value;
            return options.Storage.Provider.Equals("Json", StringComparison.OrdinalIgnoreCase)
                ? new JsonBadgeStore(sp.GetRequiredService<IOptions<DoorOptions>>())
                : new SqliteBadgeStore(sp.GetRequiredService<SqliteConnectionFactory>());
        });

        // Hardware (real on a Pi, stubbed everywhere else)
        services.AddSingleton<INfcReader>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<DoorOptions>>().Value;
            return options.Hardware.UseRealHardware
                ? ActivatorUtilities.CreateInstance<RaspberryPiNfcReader>(sp)
                : ActivatorUtilities.CreateInstance<StubNfcReader>(sp);
        });
        services.AddSingleton<IDoorRelay>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<DoorOptions>>().Value;
            return options.Hardware.UseRealHardware
                ? ActivatorUtilities.CreateInstance<GpioDoorRelay>(sp)
                : ActivatorUtilities.CreateInstance<StubDoorRelay>(sp);
        });

        // Application services
        services.AddSingleton<IActionLogService, ActionLogService>();
        services.AddSingleton<IBadgeValidationService, BadgeValidationService>();
        services.AddSingleton<IAccessRulesService, AccessRulesService>();
        services.AddSingleton<IDoorControlService, DoorControlService>();
        services.AddSingleton<IAnalyticsService, AnalyticsService>();
        services.AddSingleton<IHealthService, HealthService>();

        // Background NFC polling loop
        services.AddHostedService<NfcMonitorService>();

        return services;
    }
}
