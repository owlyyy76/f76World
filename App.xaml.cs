using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ProjectF76World.DI;

namespace f76World
{
    public partial class App : Application
    {
        private IServiceProvider _serviceProvider = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _serviceProvider = DependencyInjection.BuildContainer();

            var mainWindow = new MainWindow(_serviceProvider);
            mainWindow.Show();
        }
    }
}