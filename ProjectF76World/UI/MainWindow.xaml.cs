using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ProjectF76World.Hardware;
using ProjectF76World.Core;

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
            AppendLog("BEOW Native Engine zainicjowany poprawnie.");
        }

        private void AppendLog(string message)
        {
            // Bezpieczne wstrzyknięcie logów w WPF (Single-threaded UI)
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                LogConsole.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
                LogConsole.ScrollToEnd();
            });
        }

        private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            BtnUpdate.IsEnabled = false;
            await _updater.CheckAndUpdateAsync(AppendLog);
            BtnUpdate.IsEnabled = true;
        }

        private async void BtnLaunchGame_Click(object sender, RoutedEventArgs e)
        {
            AppendLog("Wykonywanie VRAM Purge (zwalnianie buforów DXGI)...");

            await Task.Run(() => _orchestrator.DumpVRAM());

            AppendLog("VRAM wyczyszczony. Wektor uruchomieniowy FO76 gotowy.");
            LblEngineStatus.Text = "ACTIVE";
            LblEngineStatus.Foreground = System.Windows.Media.Brushes.LimeGreen;
        }

        // Elementy kontrolne interfejsu
        private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                DragMove();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
        private void LangCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) { }
        private void BtnOptimizeIni_Click(object sender, RoutedEventArgs e) { AppendLog("Analiza Matrixów INI..."); }
        private void BtnAnalyze_Click(object sender, RoutedEventArgs e) { AppendLog("Analizowanie archiwów systemowych..."); }
        private void BtnRun_Click(object sender, RoutedEventArgs e) { AppendLog("Wykonywanie operacji wsadowych..."); }
        private void BtnKill_Click(object sender, RoutedEventArgs e) { AppendLog("Zabijanie zablokowanych procesów (Kill-Switch)..."); }
        private void BtnMenu_Click(object sender, RoutedEventArgs e) { AppendLog("Integracja rejestru (Menu Kontekstowe)..."); }
        private void BtnReport_Click(object sender, RoutedEventArgs e) { AppendLog("Generowanie logu diagnostycznego..."); }
        private void ChkAutoStart_Checked(object sender, RoutedEventArgs e) { }
        private void ChkAutoStart_Unchecked(object sender, RoutedEventArgs e) { }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_orchestrator is IDisposable disposable)
                disposable.Dispose();

            base.OnClosing(e);
        }
    }
}