using F76World.Native.Windows;
using Microsoft.Extensions.DependencyInjection;
using ProjectF76World.Core;
using ProjectF76World.Hardware;
using ProjectF76World.Hardware.Windows;
using ProjectF76World.Native.Windows;

namespace ProjectF76World.DI;

public static class DependencyInjection
{
    public static IServiceProvider BuildContainer()
    {
        var services = new ServiceCollection();

        // Rejestracja usług HTTP dla nowego API Gateway f76.world
        services.AddHttpClient();

        // Rejestracja Fluid Restart Engine
        services.AddSingleton<FluidRestartEngine>();

        // Rejestracja weryfikatora architektury (Zero-Allocation)
        services.AddSingleton<SystemArchitectureValidator>();

        // Rejestracja natywnych orkiestratorów dla klienta na Windows
        if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<IGameOrchestrator, WindowsGameOrchestrator>();
            services.AddSingleton<ITelemetryMonitor, WindowsGpuMonitor>();
        }
        else
        {
            // Fallback (np. w przypadku mockowania na XeroLinux podczas kompilacji/testów UI)
            services.AddSingleton<IGameOrchestrator, CrossPlatformGameOrchestrator>();
        }

        return services.BuildServiceProvider();
    }
}