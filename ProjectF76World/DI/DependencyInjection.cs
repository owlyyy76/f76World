using Microsoft.Extensions.DependencyInjection;
using ProjectF76World.Core;
using ProjectF76World.Hardware;
using ProjectF76World.Hardware.Windows;
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

            // Bezpośrednie podpięcie pod jądro Windows 11
            services.AddSingleton<IGameOrchestrator, WindowsGameOrchestrator>();

            return services.BuildServiceProvider();
        }
    }
}