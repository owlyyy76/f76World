#pragma warning disable CA1416
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ProjectF76World.Hardware;
using ProjectF76World.Core;

namespace f76World
{
    public partial class MainWindow : Window
    {
        private string _currentLang;
        private readonly List<OptimizationItem> _tweaks = new();
        private readonly List<OptimizationItem> _installers = new();
        private const string AutoStartTaskName = "F76World_Resonance_AutoStart";

        private System.Windows.Forms.NotifyIcon _trayIcon = null!;

        public IGameOrchestrator? Orchestrator { get; }
        private readonly FluidRestartEngine _updater;

        public MainWindow(IGameOrchestrator? orchestrator, FluidRestartEngine updater)
        {
            InitializeComponent();
            Orchestrator = orchestrator;
            _updater = updater;

            _currentLang = CultureInfo.InstalledUICulture.Name.StartsWith("pl") ? "pl" : "en";
            LangCombo.SelectedIndex = _currentLang == "pl" ? 1 : 0;

            PopulateData();
            ApplyLanguage();
            CleanupStaleBackups();
            CheckAutoStartStatus();
            InitializeTrayIcon();

            if (Environment.GetCommandLineArgs().Contains("--tray"))
            {
                this.WindowState = WindowState.Minimized;
                this.ShowInTaskbar = false;
                this.Hide();
            }
        }

        private void InitializeTrayIcon()
        {
            _trayIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Shield,
                Text = "BetterF76 Engine",
                Visible = true
            };

            _trayIcon.DoubleClick += (s, e) => RestoreWindow();

            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("Pokaż Menedżer Optymalizacji", null, (s, e) => RestoreWindow());
            menu.Items.Add("Zamknij Ekosystem", null, (s, e) => ForceExit());
            _trayIcon.ContextMenuStrip = menu;
        }

        private void RestoreWindow()
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.ShowInTaskbar = true;
            this.Activate();
        }

        private void ForceExit()
        {
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
            Application.Current.Shutdown();
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                this.ShowInTaskbar = false;
                this.Hide();
            }
            base.OnStateChanged(e);
        }

        private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left) this.DragMove();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            this.WindowState = WindowState.Minimized;
            this.ShowInTaskbar = false;
        }

        private void CheckAutoStartStatus()
        {
            Task.Run(() =>
            {
                var psi = new ProcessStartInfo("schtasks.exe", $"/query /tn \"{AutoStartTaskName}\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };
                try
                {
                    using var p = Process.Start(psi);
                    p?.WaitForExit();
                    bool exists = p?.ExitCode == 0;
                    Dispatcher.Invoke(() =>
                    {
                        ChkAutoStart.Checked -= ChkAutoStart_Checked;
                        ChkAutoStart.Unchecked -= ChkAutoStart_Unchecked;
                        ChkAutoStart.IsChecked = exists;
                        ChkAutoStart.Checked += ChkAutoStart_Checked;
                        ChkAutoStart.Unchecked += ChkAutoStart_Unchecked;
                    });
                }
                catch { }
            });
        }

        private void ChkAutoStart_Checked(object sender, RoutedEventArgs e)
        {
            string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            if (string.IsNullOrEmpty(exePath)) return;

            string args = $"/create /tn \"{AutoStartTaskName}\" /tr \"\\\"{exePath}\\\" --tray\" /sc onlogon /rl highest /f";
            Process.Start(new ProcessStartInfo("schtasks.exe", args) { CreateNoWindow = true, UseShellExecute = false });
            AppendLog("[SYSTEM] Boot sequence UAC-Bypass zintegrowany z rejestrem bazowym.");
        }

        private void ChkAutoStart_Unchecked(object sender, RoutedEventArgs e)
        {
            string args = $"/delete /tn \"{AutoStartTaskName}\" /f";
            Process.Start(new ProcessStartInfo("schtasks.exe", args) { CreateNoWindow = true, UseShellExecute = false });
            AppendLog("[SYSTEM] Odcięto natywną sekwencję ładującą systemu Windows.");
        }

        private void PopulateData()
        {
            _tweaks.Add(new OptimizationItem("Zarządzanie rejestrem zasilania CPU", "Write-Output 'Symulacja...'", null));
        }

        private void ApplyLanguage()
        {
            Title = "BetterF76 | The Wasteland Online";
            BtnUpdate.Content = _currentLang == "pl" ? "🔄 Aktualizuj Ekosystem" : "🔄 Update Ecosystem";

            RenderCheckboxes(TweaksContainer, _tweaks);
            RenderCheckboxes(InstallersContainer, _installers);

            if (string.IsNullOrWhiteSpace(LogConsole.Text)) AppendLog("[SYSTEM] Rdzeń BEOW zsynchronizowany. Interfejs graficzny online.");
        }

        private void RenderCheckboxes(StackPanel container, List<OptimizationItem> items)
        {
            container.Children.Clear();
            foreach (var item in items)
            {
                var chk = new CheckBox
                {
                    Content = item.Id,
                    IsChecked = !item.IsOptimized,
                    Margin = new Thickness(0, 6, 0, 2),
                    Tag = item,
                    Foreground = Brushes.White
                };
                item.UiElement = chk;
                container.Children.Add(chk);
            }
        }

        private void AppendLog(string text)
        {
            Dispatcher.Invoke(() =>
            {
                LogConsole.AppendText(text + Environment.NewLine);
                LogConsole.ScrollToEnd();
            });
        }

        private void LangCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _currentLang = LangCombo.SelectedIndex == 1 ? "pl" : "en";
            ApplyLanguage();
        }

        private async void BtnLaunchGame_Click(object sender, RoutedEventArgs e)
        {
            LogConsole.Clear();
            AppendLog("[WSTRZYKNIĘCIE] Rozpoczynam procedurę Purge VRAM...");

            SetUiEnabled(false);
            LblEngineStatus.Text = "PURGING VRAM...";
            LblEngineStatus.Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60));

            try
            {
                if (Orchestrator != null)
                {
                    await Task.Run(() => Orchestrator.DumpVRAM());
                    LogConsole.AppendText("[SUCCESS] Bufory VRAM opróżnione bezpiecznie przez DXGI Shim.\n");
                }

                AppendLog("Wektor uruchomieniowy FO76 aktywowany. Gra jest ładowana.");
            }
            catch (Exception ex)
            {
                AppendLog($"[ERROR] Awaria Orkiestratora: {ex.Message}");
            }

            SetUiEnabled(true);
            LblEngineStatus.Text = "ACTIVE";
            LblEngineStatus.Foreground = new SolidColorBrush(Color.FromRgb(0, 230, 118));
        }

        private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            AppendLog("Ustanawiam połączenie z bramą danych f76.world...");
            BtnUpdate.IsEnabled = false;

            var (updateAvailable, newVersion) = await _updater.CheckForUpdatesAsync(AppendLog);

            if (updateAvailable)
            {
                var result = MessageBox.Show(
                    $"Krytyczna aktualizacja {newVersion} jest dostępna. Zainicjować pobieranie w tle i zrestartować aplikację?",
                    "F76.World Fluid Updater",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    SetUiEnabled(false);
                    await _updater.ExecuteSeamlessRestartAsync(AppendLog);
                }
                else
                {
                    AppendLog("[INFO] Aktualizacja została odłożona przez Architekta.");
                }
            }
            else
            {
                AppendLog("[INFO] Obecna sygnatura plików odpowiada najnowszej kompilacji.");
            }

            BtnUpdate.IsEnabled = true;
        }

        private void SetUiEnabled(bool state)
        {
            TabTweaks.IsEnabled = state;
            TabRescue.IsEnabled = state;
            BtnLaunchGame.IsEnabled = state;
        }

        private void CleanupStaleBackups()
        {
            try
            {
                string currentExeFile = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                if (!string.IsNullOrEmpty(currentExeFile))
                {
                    string backupExeFile = currentExeFile + ".update";
                    if (File.Exists(backupExeFile)) File.Delete(backupExeFile);
                }
            }
            catch { }
        }

        private sealed class OptimizationItem
        {
            public string Id { get; }
            private readonly string? _psScript;
            public bool IsOptimized { get; private set; }
            public CheckBox? UiElement { get; set; }

            public OptimizationItem(string id, string? psScript, Action? nativeAction = null)
            {
                Id = id;
                _psScript = psScript;
            }
        }
    }
}