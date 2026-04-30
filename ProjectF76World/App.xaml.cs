#if false
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;
using ProjectF76World.DI;
using ProjectF76World.Hardware;
using ProjectF76World.Hardware.Linux;
using RabbitMQ.Client.Events;
using System.Windows;

namespace ProjectF76World;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;

    // DI Container Setup - Cross-Platform Routing
    protected override void Initialize()
    {
        var services = new ServiceCollection();

        // Register core orchestrator (Cross-platform wrapper)
        services.AddSingleton<IGameOrchestrator, CrossPlatformGameOrchestrator>();

        // OS-specific service registration based on platform detection
        if (OperatingSystem.IsLinux())
        {
            // Linux: AMD GPU monitor implementation
            services.AddSingleton<ILinuxAmdGpuMonitor, LinuxAmdGpuMonitor>();
            
            // Register orchestrator with LinuxAmdGpuMonitor injection
            DependencyInjection.RegisterLinuxOrchestrator(services);

            // Telemetry routing to Linux implementation
            services.AddSingleton<ITelemetryMonitor>(service => 
                service.GetRequiredService<ILinuxAmdGpuMonitor>());
        }
        else if (OperatingSystem.IsWindows())
        {
            // Windows: PerformanceCounter-based GPU monitoring
            DependencyInjection.RegisterWindowsOrchestrator(services);

            // Register orchestrator with IKernelTelemetryMonitor injection
            services.AddSingleton<IKernelTelemetryMonitor, WindowsGameOrchestrator>();

            // Telemetry routing to Windows implementation
            services.AddSingleton<ITelemetryMonitor>(service => 
                service.GetRequiredService<IKernelTelemetryMonitor>());
        }

        // Register core interfaces for dependency injection resolution
        services.AddSingleton<ITelemetryMonitor>();
        services.AddSingleton<IGameOrchestrator>();

        _serviceProvider = services.BuildServiceProvider();
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // OS-specific initialization hooks
        if (OperatingSystem.IsLinux())
            _serviceProvider.GetRequiredService<ILinuxAmdGpuMonitor>().InitializeLinuxPath();
        else if (OperatingSystem.IsWindows())
            _serviceProvider.GetRequiredService<IKernelTelemetryMonitor>().InitializeWindowsKernelMode();

        // Start background telemetry loop via orchestrator
        _serviceProvider.GetRequiredService<IGameOrchestrator>()
            .StartBackgroundMonitoringLoop();
    }

    protected override void OnExit(ShutdownEventArgs e)
    {
        base.OnExit(e);

        if (OperatingSystem.IsLinux())
            _serviceProvider.GetRequiredService<ILinuxAmdGpuMonitor>().CleanupLinuxResources();
        else if (OperatingSystem.IsWindows())
            _serviceProvider.GetRequiredService<IKernelTelemetryMonitor>().CleanupWindowsResources();
    }
}
#endif
