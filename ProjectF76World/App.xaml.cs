using System;
using System.Windows;
using ProjectF76World.DI;

namespace ProjectF76World;

/// <summary>
/// Logika startowa aplikacji WPF dla systemu Windows pod kontrolą Visual Studio 2026.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Inicjalizacja rdzenia wstrzykiwania zależności przed załadowaniem pierwszego widoku UI
        DependencyInjection.Initialize();
    }
}