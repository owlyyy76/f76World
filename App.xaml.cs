using System;
using System.Configuration;
using System.Data;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using f76World.Native.Core.Execution;
using f76World.Native.Core.Telemetry;

namespace f76World
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // BEOW Engine Integration: Initialize Dependency Injection Container
            var services = new ServiceCollection();

            // Register Core Execution Modules as Singletons (use existing cross-platform orchestrator if present)
            // If GameOrchestrator implementation isn't available, fall back to no-op CrossPlatformGameOrchestrator
            try
            {
                // Prefer concrete GameOrchestrator type if present in the solution
                var gameOrchType = Type.GetType("GameOrchestrator");
                if (gameOrchType != null)
                    services.AddSingleton(typeof(IGameOrchestrator), gameOrchType);
                else
                    services.AddSingleton<IGameOrchestrator, f76World.Native.Core.Execution.NoopGameOrchestrator>();
            }
            catch
            {
                services.AddSingleton<IGameOrchestrator, f76World.Native.Core.Execution.NoopGameOrchestrator>();
            }

            // Register Telemetry Monitor (Module C - AMD GPU)
            services.AddSingleton<ITelemetryMonitor, AmdGpuMonitor>();

            // Register additional engine modules (placeholder for future plugins)
            services.AddTransient<IBethesdaIniParser, f76World.Native.Core.Execution.NoopBethesdaIniParser>();

            // Build the DI container
            var provider = services.BuildServiceProvider();

            // Store provider for later retrieval via a static property on App
            // (avoid adding members to System.Windows.Application)
            ApplicationServices.Provider = provider;
        }
    }
}
