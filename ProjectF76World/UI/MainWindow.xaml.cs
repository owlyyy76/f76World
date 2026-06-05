using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ProjectF76World.Core;
using ProjectF76World.Hardware;
using ProjectF76World.Native.Windows;
using ProjectF76World.Native.Windows.Registry;

namespace ProjectF76World.UI;

/// <summary>
/// Główny interfejs sterowania launcherem. Zintegrowany z bezblokadowym silnikiem aktualizacji.
/// </summary>
public partial class MainWindow : Window
{
    private readonly RegistryRollbackManager _rollbackManager;
    private readonly IGameOrchestrator _gameOrchestrator;
    private readonly FluidRestartEngine _updateEngine;

    public MainWindow()
    {
        InitializeComponent();

        // Pobieranie bezpiecznych, zindeksowanych usług z kontenera DI
        var services = ProjectF76World.DI.DependencyInjection.ServiceProvider;
        _gameOrchestrator = services.GetRequiredService<IGameOrchestrator>();
        _updateEngine = services.GetRequiredService<FluidRestartEngine>();

        _rollbackManager = new RegistryRollbackManager();

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Sprawdzenie integralności architektury (Tylko dla środowiska Windows)
        if (OperatingSystem.IsWindows())
        {
            try
            {
                SystemArchitectureValidator.EnsureSystemReadiness();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Krytyczny błąd systemu", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Automatyczne i ciche sprawdzenie dostępności aktualizacji przy starcie
        await CheckForUpdatesSilentlyAsync();
    }

    /// <summary>
    /// Metoda asynchroniczna sprawdzająca stan binarnego repozytorium f76.world.
    /// </summary>
    private async Task CheckForUpdatesSilentlyAsync()
    {
        bool updateAvailable = await _updateEngine.CheckForUpdatesAsync();
        if (updateAvailable)
        {
            var result = MessageBox.Show(
                "Wykryto nową, stabilną wersję launchera BetterF76. Czy chcesz zaktualizować aplikację automatycznie?",
                "Dostępna Aktualizacja",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Uruchomienie procedury bezblokadowego nadpisywania binarnego
                bool success = await _updateEngine.ExecuteUpdateAsync();
                if (!success)
                {
                    MessageBox.Show("Wystąpił błąd podczas pobierania paczki aktualizacyjnej.", "Błąd Aktualizacji", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private void OnDisableMpoClick(object sender, RoutedEventArgs e)
    {
        if (!OperatingSystem.IsWindows()) return;

        Dispatcher.Invoke(() =>
        {
            bool success = _rollbackManager.ApplyOptimization(NativeRegistry.HKEY_LOCAL_MACHINE, @"SOFTWARE\Microsoft\Windows\Dwm", "OverlayTestMode", 5u);
            if (success) MessageBox.Show("MPO zostało pomyślnie wyłączone.", "Optymalizacja", MessageBoxButton.OK, MessageBoxImage.Information);
        });
    }

    private void OnClearMemoryClick(object sender, RoutedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            bool optimized = _gameOrchestrator.OptimizeHardwareResources();
            if (optimized)
            {
                MessageBox.Show("Pamięć podręczna aplikacji w tle została pomyślnie zrzucona.", "Optymalizacja VRAM/RAM", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        });
    }

    private void OnRollbackClick(object sender, RoutedEventArgs e)
    {
        if (!OperatingSystem.IsWindows()) return;

        Dispatcher.Invoke(() =>
        {
            _rollbackManager.RollbackAll();
            MessageBox.Show("Przywrócono domyślne ustawienia rejestru systemowego.", "Rollback", MessageBoxButton.OK, MessageBoxImage.Information);
        });
    }
}