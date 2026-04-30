using Microsoft.Extensions.DependencyInjection;
using ProjectF76World.Hardware.Linux;
using ProjectF76World.Hardware.Windows;

namespace ProjectF76World.DI;

/// <summary>
/// Dependency Injection container builder for cross-platform service registration.
/// Implements dynamic OS routing for telemetry and orchestrator services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Build the DI container with all services registered.
    /// </summary>
    public static IServiceProvider BuildContainer()
    {
        var services = new ServiceCollection();

        // Register core orchestrator (Cross-platform wrapper)
        services.AddSingleton<IGameOrchestrator, CrossPlatformGameOrchestrator>();
        
        // GPU Telemetry - Runtime OS Routing via ITelemetryMonitor interface
        if (OperatingSystem.IsLinux()) 
            services.AddSingleton<ITelemetryMonitor>(service => service.GetRequiredService<ILinuxAmdGpuMonitor>());
        else if (OperatingSystem.IsWindows()) 
            services.AddSingleton<ITelemetryMonitor, WindowsGpuMonitor>();

        // Register IKernelTelemetryMonitor for orchestrator injection
        if (OperatingSystem.IsLinux()) 
            services.AddSingleton<IKernelTelemetryMonitor>(service => service.GetRequiredService<ILinuxAmdGpuMonitor>());
        else if (OperatingSystem.IsWindows()) 
            services.AddSingleton<IKernelTelemetryMonitor, WindowsGameOrchestrator>();

        // Register core interfaces for DI resolution
        services.AddSingleton<ITelemetryMonitor>();
        services.AddSingleton<IGameOrchestrator>();
        
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Register cross-platform services into a service collection.
    /// </summary>
    public static void RegisterCrossPlatformServices(IServiceCollection services)
    {
        // Core orchestrator with OS-specific injection points
        services.AddSingleton<IGameOrchestrator, CrossPlatformGameOrchestrator>();

        // Linux-specific AMD GPU monitor (if applicable)
        if (OperatingSystem.IsLinux()) 
            services.AddSingleton<ILinuxAmdGpuMonitor, LinuxAmdGpuMonitor>();

        // Windows stub implementation (always available for fallback)
        services.AddSingleton<IKernelTelemetryMonitor>(service => new WindowsGameOrchestrator());
    }

    /// <summary>
    /// Register platform-specific telemetry interfaces.
    /// </summary>
    public static void RegisterPlatformSpecificServices(IServiceCollection services)
    {
        if (OperatingSystem.IsLinux()) 
            services.AddSingleton<ITelemetryMonitor, LinuxAmdGpuMonitor>();
        else if (OperatingSystem.IsWindows()) 
            services.AddSingleton<ITelemetryMonitor>(service => new WindowsGpuMonitor());
    }

    /// <summary>
    /// Register core services with OS-aware routing.
    /// </summary>
    public static void RegisterCoreServices(IServiceCollection services)
    {
        // Core game orchestrator with OS-aware routing
        services.AddSingleton<IGameOrchestrator, CrossPlatformGameOrchestrator>();

        // GPU Telemetry - Dynamic Selection Based on Operating System
        if (OperatingSystem.IsLinux()) 
            services.AddSingleton<ITelemetryMonitor>(service => service.GetRequiredService<ILinuxAmdGpuMonitor>());
        else 
            services.AddSingleton<ITelemetryMonitor, WindowsGpuMonitor>();
    }

    /// <summary>
    /// Register Linux-specific kernel telemetry interface.
    /// </summary>
    public static void RegisterLinuxKernelServices(IServiceCollection services)
    {
        if (OperatingSystem.IsLinux())
        {
            services.AddSingleton<ILinuxAmdGpuMonitor, LinuxAmdGpuMonitor>();
            services.AddSingleton<IKernelTelemetryMonitor>(service => service.GetRequiredService<ILinuxAmdGpuMonitor>());
        }
    }

    /// <summary>
    /// Register Windows-specific kernel telemetry interface.
    /// </summary>
    public static void RegisterWindowsKernelServices(IServiceCollection services)
    {
        if (OperatingSystem.IsWindows())
        {
            // WindowsGameOrchestrator implements IKernelTelemetryMonitor via inheritance
            services.AddSingleton<IKernelTelemetryMonitor, WindowsGameOrchestrator>();
        }
    }

    /// <summary>
    /// Explicitly register Windows-specific orchestrator for direct injection.
    /// </summary>
    public static void RegisterWindowsOrchestrator(IServiceCollection services)
    {
        if (OperatingSystem.IsWindows())
        {
            // Register WindowsGameOrchestrator as IKernelTelemetryMonitor implementation
            services.AddSingleton<IKernelTelemetryMonitor, WindowsGameOrchestrator>();
        }
    }

    /// <summary>
    /// Explicitly register Linux-specific orchestrator for direct injection.
    /// </summary>
    public static void RegisterLinuxOrchestrator(IServiceCollection services)
    {
        if (OperatingSystem.IsLinux())
        {
            // Register LinuxAmdGpuMonitor as IKernelTelemetryMonitor implementation
            services.AddSingleton<IKernelTelemetryMonitor, LinuxAmdGpuMonitor>();
        }
    }
}
