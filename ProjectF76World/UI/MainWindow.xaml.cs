using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using ProjectF76World.Core;
using ProjectF76World.Hardware;

namespace f76World
{
    public partial class MainWindow : Window
    {
        private readonly IGameOrchestrator _orchestrator;
        private readonly FluidRestartEngine _updater;

        public MainWindow(IServiceProvider serviceProvider)
        {
            InitializeComponent();

            _orchestrator = serviceProvider.GetRequiredService<IGameOrchestrator>();
            _updater = serviceProvider.GetRequiredService<FluidRestartEngine>();

            _orchestrator.Initialize();
            AppendLog("[SYSTEM] Architektura Windows 11 Native zablokowana. Silnik gotowy.");
        }

        private void AppendLog(string message)
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                LogConsole.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
                LogConsole.ScrollToEnd();
            });
        }

        private async void BtnLaunchGame_Click(object sender, RoutedEventArgs e)
        {
            BtnLaunchGame.IsEnabled = false;
            AppendLog("[WSTRZYKNIĘCIE] Rozpoczynam procedurę Purge VRAM poprzez DXGI Shim...");

            LblEngineStatus.Text = "PURGING VRAM...";
            LblEngineStatus.Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60));

            try
            {
                await Task.Run(() => _orchestrator.DumpVRAM());
                AppendLog("[SUCCESS] Bufory wyczyszczone. Gra uruchomiona w czystym środowisku.");

                LblEngineStatus.Text = "ACTIVE";
                LblEngineStatus.Foreground = new SolidColorBrush(Color.FromRgb(0, 230, 118));
            }
            catch (Exception ex)
            {
                AppendLog($"[ERROR] Błąd Orkiestratora Jądra: {ex.Message}");
            }
            finally
            {
                BtnLaunchGame.IsEnabled = true;
            }
        }

        private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            BtnUpdate.IsEnabled = false;
            await _updater.CheckAndUpdateAsync(AppendLog);
            BtnUpdate.IsEnabled = true;
        }

        // --- Kontrolki paska tytułowego ---
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}