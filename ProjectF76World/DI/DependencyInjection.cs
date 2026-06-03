using Microsoft.Extensions.DependencyInjection;
using ProjectF76World.Hardware.Windows;
using ProjectF76World.Hardware;
using ProjectF76World.Core;
using System;

namespace ProjectF76World.DI
{
    public static class DependencyInjection
    {
        public static IServiceProvider BuildContainer()
        {
            var services = new ServiceCollection();

            services.AddHttpClient();
            services.AddSingleton<FluidRestartEngine>();

            if (OperatingSystem.IsWindows())
            {
                services.AddSingleton<IGameOrchestrator, WindowsGameOrchestrator>();
                services.AddSingleton<IKernelTelemetryMonitor, WindowsGameOrchestrator>();
            }

            return services.BuildServiceProvider();
        }
    }
}