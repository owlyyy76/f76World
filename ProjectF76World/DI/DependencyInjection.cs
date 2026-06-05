using System;
using Microsoft.Extensions.DependencyInjection;
using ProjectF76World.Core;
using ProjectF76World.Hardware;
using ProjectF76World.Hardware.Windows;

namespace ProjectF76World.DI;

/// <summary>
/// Zunifikowany kontener DI zapewniający deterministyczne ładowanie usług hardware i core.
/// Izoluje zależności systemowe Windows/Linux w celu zapewnienia stabilności kompilacji i działania.
/// </summary>
public static class DependencyInjection
{
    private static IServiceProvider? _serviceProvider;

    public static IServiceProvider ServiceProvider
    {
        get
        {
            if (_serviceProvider == null)
            {
                throw new InvalidOperationException("Kontener DI nie został zainicjalizowany. Wywołaj najpierw Initialize().");
            }
            return _serviceProvider;
        }
    }

    /// <summary>
    /// Buduje graf zależności na podstawie wykrytego środowiska operacyjnego.
    /// </summary>
    public static void Initialize()
    {
        var services = new ServiceCollection();

        // 1. Rejestracja Silnika Aktualizacji (Niezależny od Platformy)
        services.AddSingleton<FluidRestartEngine>();

        // 2. Warunkowa rejestracja orkiestratora i telemetryka systemowego
        if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<IGameOrchestrator, WindowsGameOrchestrator>();
        }
        else
        {
            services.AddSingleton<IGameOrchestrator, CrossPlatformGameOrchestrator>();
        }

        _serviceProvider = services.BuildServiceProvider();
    }
}