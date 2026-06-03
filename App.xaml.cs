using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ProjectF76World.DI;
using ProjectF76World.Hardware;
using ProjectF76World.Core;

namespace f76World
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var services = DependencyInjection.BuildContainer();

            // Bezpośrednie wstrzyknięcie zunifikowanych zależności
            var mainWindow = new MainWindow(
                services.GetService<IGameOrchestrator>(),
                services.GetRequiredService<FluidRestartEngine>()
            );

            mainWindow.Show();
        }
    }
}