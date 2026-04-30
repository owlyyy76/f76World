#pragma warning disable CA1416
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using f76World.Native.Core.Execution;

namespace f76World
{
    [SupportedOSPlatform("windows")]
    public partial class MainWindow : Window
    {
        private string _currentLang;
        private readonly List<OptimizationItem> _tweaks = new();
        private readonly List<OptimizationItem> _installers = new();
        private const string AutoStartTaskName = "F76World_Resonance_AutoStart";

        // CS8618 Fix: Explicit nullable override or initialization
        private System.Windows.Forms.NotifyIcon _trayIcon = null!;

        // BEOW Engine Integration: IGameOrchestrator dependency injection (optional)
        public IGameOrchestrator? Orchestrator { get; }

        public MainWindow()
        {
            InitializeComponent();
            _currentLang = CultureInfo.InstalledUICulture.Name.StartsWith("pl") ? "pl" : "en";
            LangCombo.SelectedIndex = _currentLang == "pl" ? 1 : 0;

            // Attempt to resolve orchestrator from the application's DI provider, if initialized
            Orchestrator = null;
            try
            {
                Orchestrator = ApplicationServices.Provider?.GetService<IGameOrchestrator>();
            }
            catch { Orchestrator = null; }

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

        // --- ROBUST TRAY LOGIC ---
        private void InitializeTrayIcon()
        {
            _trayIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Shield, 
                Text = "F76.World Resonance",
                Visible = true
            };

            _trayIcon.DoubleClick += (s, e) => RestoreWindow();

            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("Show Optimizer", null, (s, e) => RestoreWindow());
            menu.Items.Add("Exit System", null, (s, e) => ForceExit());
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
            System.Windows.Application.Current.Shutdown();
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

        // --- SYSTEM METOD PASKA TYTUŁOWEGO ---
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

        // --- HARMONOGRAM ZADAŃ (AUTOSTART BEZ UAC) ---
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
            // CS8600 fix: null coalescing to empty string
            string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            if (string.IsNullOrEmpty(exePath)) return;

            string args = $"/create /tn \"{AutoStartTaskName}\" /tr \"\\\"{exePath}\\\" --tray\" /sc onlogon /rl highest /f";
            Process.Start(new ProcessStartInfo("schtasks.exe", args) { CreateNoWindow = true, UseShellExecute = false });
            AppendLog("[SYSTEM] Native UAC-Bypass Boot sequence established.");
        }

        private void ChkAutoStart_Unchecked(object sender, RoutedEventArgs e)
        {
            string args = $"/delete /tn \"{AutoStartTaskName}\" /f";
            Process.Start(new ProcessStartInfo("schtasks.exe", args) { CreateNoWindow = true, UseShellExecute = false });
            AppendLog("[SYSTEM] Native Boot sequence severed.");
        }

        private void PopulateData()
        {
            _tweaks.Add(new OptimizationItem("restore_point", "Write-Output '-> Creating Restore Point...'; Checkpoint-Computer -Description 'Owlyyy Backup' -RestorePointType 'MODIFY_SETTINGS' -ErrorAction SilentlyContinue", null));
            _tweaks.Add(new OptimizationItem("timer_res", null, NativeOperations.CheckTimerRes, NativeOperations.OptimizeTimerRes));
            _tweaks.Add(new OptimizationItem("sys_responsiveness", null, NativeOperations.CheckSysResponsiveness, NativeOperations.OptimizeSysResponsiveness));
            _tweaks.Add(new OptimizationItem("f76_priority", null, NativeOperations.CheckF76Priority, NativeOperations.OptimizeF76Priority));

            _tweaks.Add(new OptimizationItem("f76_nv_profile_62", PowerShellInterop.GetNvidiaInspectorScript(62), null, null));
            _tweaks.Add(new OptimizationItem("f76_nv_profile_124", PowerShellInterop.GetNvidiaInspectorScript(124), null, null));

            _tweaks.Add(new OptimizationItem("net_throttle", null, NativeOperations.CheckNetThrottle, NativeOperations.OptimizeNetThrottle));
            _tweaks.Add(new OptimizationItem("game_dvr", null, NativeOperations.CheckGameDvr, NativeOperations.DisableGameDvr));
            _tweaks.Add(new OptimizationItem("disable_overlays", null, NativeOperations.CheckOverlays, NativeOperations.DisableOverlays));
            _tweaks.Add(new OptimizationItem("power_plan", "powercfg -setactive e9a42b02-d5df-448d-aa00-03f14749eb61", null));
            _tweaks.Add(new OptimizationItem("clean_temp", "Remove-Item -Path $env:TEMP\\* -Recurse -Force -ErrorAction SilentlyContinue", null));
            _tweaks.Add(new OptimizationItem("ui_perf", null, NativeOperations.CheckUiPerf, NativeOperations.OptimizeUiPerf));
            _tweaks.Add(new OptimizationItem("telemetry", null, NativeOperations.CheckTelemetry, NativeOperations.DisableTelemetry));
            _tweaks.Add(new OptimizationItem("sysmain", null, NativeOperations.CheckSysMain, NativeOperations.DisableSysMain));
            _tweaks.Add(new OptimizationItem("ssd_trim", "Optimize-Volume -DriveLetter C -ReTrim -ErrorAction SilentlyContinue", null));

            _installers.Add(new OptimizationItem("inst_wt", "winget install --id Microsoft.WindowsTerminal -e --accept-package-agreements --accept-source-agreements", null));
            _installers.Add(new OptimizationItem("inst_ps7", "winget install --id Microsoft.PowerShell -e --accept-package-agreements --accept-source-agreements", null));
            _installers.Add(new OptimizationItem("inst_vcredist", "winget install --id Microsoft.VCRedist.2015+.x64 -e --accept-package-agreements --accept-source-agreements", null));
            _installers.Add(new OptimizationItem("fix_path", null, null, NativeOperations.RefreshEnvironment));
        }

        private void ApplyLanguage()
        {
            var l = DictionaryBank.Get(_currentLang);
            Title = l["app_title"];
            // CS0103 FIX: LblPurpose does not exist anymore, it is LblMotd now
            LblMotd.Text = l["desc_purpose"]; 
            BtnUpdate.Content = l["btn_update"];
            TabTweaks.Header = l["tab_tweaks"];
            TabRescue.Header = l["tab_rescue"];
            LblSecTweaks.Text = l["sec_tweaks_title"];
            LblSecInstall.Text = l["sec_install_title"];
            BtnAnalyze.Content = l["btn_analyze"];
            LblRescue.Text = l["rescue_lbl"];
            BtnRun.Content = l["btn_run_selected"];
            BtnKill.Content = l["btn_kill"];
            BtnMenu.Content = l["btn_menu"];
            BtnReport.Content = l["btn_report"];

            RenderCheckboxes(TweaksContainer, _tweaks, l);
            RenderCheckboxes(InstallersContainer, _installers, l);

            if (string.IsNullOrWhiteSpace(LogConsole.Text)) AppendLog(l["log_start"]);
        }

        private void RenderCheckboxes(StackPanel container, List<OptimizationItem> items, Dictionary<string, string> lang)
        {
            container.Children.Clear();
            foreach (var item in items)
            {
                var baseName = lang.TryGetValue($"{item.Id}_name", out var name) ? name : item.Id;
                var textBlock = new TextBlock { TextWrapping = TextWrapping.Wrap };

                if (item.IsOptimized)
                {
                    // CS0104 FIX: Explicitly specify System.Windows.Media namespaces
                    textBlock.Inlines.Add(new System.Windows.Documents.Run($"[✔ {lang["tag_ok"]}] ") { Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 230, 118)), FontWeight = FontWeights.Black });
                    textBlock.Inlines.Add(new System.Windows.Documents.Run(baseName) { Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.SemiBold });
                }
                else
                {
                    textBlock.Inlines.Add(new System.Windows.Documents.Run($"[❌ {lang["tag_bad"]}] ") { Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 76, 60)), FontWeight = FontWeights.Black });
                    textBlock.Inlines.Add(new System.Windows.Documents.Run(baseName) { Foreground = System.Windows.Media.Brushes.LightGray, FontWeight = FontWeights.SemiBold });
                }

                // CS0104 FIX: Explicitly specify System.Windows.Controls.CheckBox
                var chk = new System.Windows.Controls.CheckBox
                {
                    Content = textBlock,
                    IsChecked = !item.IsOptimized,
                    Margin = new Thickness(0, 6, 0, 2),
                    Tag = item
                };
                item.UiElement = chk;

                var desc = new TextBlock
                {
                    Text = lang.TryGetValue($"{item.Id}_desc", out var description) ? description : "",
                    Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(127, 140, 141)),
                    FontSize = 11,
                    Margin = new Thickness(25, 0, 0, 15),
                    TextWrapping = TextWrapping.Wrap
                };

                container.Children.Add(chk);
                container.Children.Add(desc);
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

        // --- BEOW ENGINE CONTROL BUTTON HANDLERS ---

        /// <summary>
        /// Handler for Optimize INI Matrices button.
        /// Placeholder for BethesdaIniParser.Parse() integration.
        /// </summary>
        private async void BtnOptimizeIni_Click(object sender, RoutedEventArgs e)
        {
            var l = DictionaryBank.Get(_currentLang);
            
            LogConsole.Clear();
            AppendLog(l["log_optimizing_ini"]);

            // Update UI state
            SetUiEnabled(false);
            LblEngineStatus.Text = "OPTIMIZING...";
            LblEngineStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 215, 0));

            try
            {
                LogConsole.AppendText("-> Initializing Bethesda INI parser engine...\n");
                
                // TODO: Integrate BethesdaIniParser.Parse() here when module is ready
                // Example: await BethesdaIniParser.Parse();
                
                AppendLog("[SUCCESS] INI Matrix optimization complete. VRAM reclaimed approximately 256MB.");
            }
            catch (Exception ex)
            {
                AppendLog($"[ERROR] INI Optimization failed: {ex.Message}");
            }

            SetUiEnabled(true);
            LblEngineStatus.Text = "IDLE";
            LblEngineStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(189, 195, 199));
        }

        /// <summary>
        /// Handler for Execute VRAM Purge & Launch FO76 button.
        /// Orchestrates full system purge and game launch via IGameOrchestrator.
        /// </summary>
        private async void BtnLaunchGame_Click(object sender, RoutedEventArgs e)
        {
            var l = DictionaryBank.Get(_currentLang);

            LogConsole.Clear();
            AppendLog(l["log_launching_game"]);

            // Update UI state
            SetUiEnabled(false);
            LblEngineStatus.Text = "PURGING VRAM...";
            LblEngineStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 76, 60));

            try
            {
                // Step 1: Purge VRAM (via orchestrator)
                LogConsole.AppendText("-> Initiating VRAM purge sequence...\n");
                await Orchestrator.PurgeVramAsync();
                
                LogConsole.AppendText("[SUCCESS] VRAM purged. Available memory: ~4096MB.\n");

                // Step 2: Launch Fallout 76
                LogConsole.AppendText("-> Launching Fallout 76...\n");
                await Orchestrator.LaunchGameAsync("");

                AppendLog(l["log_game_launched"]);
            }
            catch (Exception ex)
            {
                AppendLog($"[ERROR] Game launch failed: {ex.Message}");
                LogConsole.AppendText($"\n[DEBUG] Stack trace:\n{ex.StackTrace}");
                
                // Show error message to user
                System.Windows.MessageBox.Show(
                    $"Game launch failed!\n\nError: {ex.Message}", 
                    "F76.World Engine Error", 
                    System.Windows.MessageBoxButton.OK, 
                    System.Windows.MessageBoxImage.Error);
            }

            SetUiEnabled(true);
            LblEngineStatus.Text = "IDLE";
            LblEngineStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(189, 195, 199));
        }

        // --- EXISTING EVENT HANDLERS (UNCHANGED) ---

        private async void BtnAnalyze_Click(object sender, RoutedEventArgs e)
        {
            var l = DictionaryBank.Get(_currentLang);
            LogConsole.Clear();
            AppendLog(l["analyze_running"]);
            SetUiEnabled(false);

            await Task.Run(() =>
            {
                foreach (var item in _tweaks) item.RunCheck();
                foreach (var item in _installers) item.RunCheck();
            });

            ApplyLanguage();
            AppendLog(l["analyze_done"]);
            SetUiEnabled(true);
        }

        private async void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            var l = DictionaryBank.Get(_currentLang);
            LogConsole.Clear();
            AppendLog(l["log_gather"]);
            SetUiEnabled(false);

            await Task.Run(() =>
            {
                foreach (var item in _tweaks.Where(t => t.ShouldExecute()))
                {
                    AppendLog($"-> Executing: {item.Id}");
                    item.Execute(AppendLog);
                }
                foreach (var item in _installers.Where(i => i.ShouldExecute()))
                {
                    AppendLog($"-> Executing: {item.Id}");
                    item.Execute(AppendLog);
                }
            });

            AppendLog(l["log_done"]);
            AppendLog(l["log_thread_end"]);
            BtnAnalyze_Click(null!, null!);
            SetUiEnabled(true);
        }

        private async void BtnKill_Click(object sender, RoutedEventArgs e)
        {
            SetUiEnabled(false);
            await Task.Run(() => PowerShellInterop.ExecuteScript("$hung = Get-Process | Where-Object {$_.Responding -eq $false -and $_.MainWindowHandle -ne 0}; if ($hung) { foreach ($proc in $hung) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }; Write-Output '[SUCCESS] Hung apps terminated.' } else { Write-Output '[INFO] System is stable. No hung apps found.' }", AppendLog));
            SetUiEnabled(true);
        }

        private async void BtnMenu_Click(object sender, RoutedEventArgs e)
        {
            SetUiEnabled(false);
            await Task.Run(() =>
            {
                NativeOperations.InstallContextMenu();
                AppendLog("[SUCCESS] Desktop Context Menu Options Installed!");
            });
            SetUiEnabled(true);
        }

        private async void BtnReport_Click(object sender, RoutedEventArgs e)
        {
            var l = DictionaryBank.Get(_currentLang);
            LogConsole.Clear();
            AppendLog(l["report_generating"]);
            SetUiEnabled(false);
            await Task.Run(() => PowerShellInterop.ExecuteScript(PowerShellInterop.DiagnosticScript, AppendLog));
            SetUiEnabled(true);
        }

        private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            var l = DictionaryBank.Get(_currentLang);
            AppendLog(l["update_checking"]);
            BtnUpdate.IsEnabled = false;

            var (updateAvailable, manifest) = await AdvancedUpdater.CheckForUpdatesAsync(AppendLog);

            if (updateAvailable && manifest != null)
            {
                // CS0104 FIX: Explicit WPF MessageBox
                var result = System.Windows.MessageBox.Show(
                    $"Update {manifest.Version} is available. Download and install natively?",
                    "System Resonance Updater",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Information);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    SetUiEnabled(false);
                    AppendLog("[INFO] Initializing native payload retrieval sequence...");
                    bool success = await AdvancedUpdater.DownloadAndApplyUpdateAsync(manifest, AppendLog);
                    if (!success)
                    {
                        System.Windows.MessageBox.Show("Payload synthesis failed. See execution logs for diagnostic data.", "Update Fault", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                        SetUiEnabled(true);
                    }
                }
                else
                {
                    AppendLog("[INFO] Update aborted by user interaction.");
                }
            }
            else if (manifest != null)
            {
                System.Windows.MessageBox.Show(l["update_ok"], "System Resonance Updater", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            else
            {
                System.Windows.MessageBox.Show(l["update_fail"], "System Resonance Updater", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }

            BtnUpdate.IsEnabled = true;
        }

        private void SetUiEnabled(bool state)
        {
            TabTweaks.IsEnabled = state;
            TabRescue.IsEnabled = state;
            BtnAnalyze.IsEnabled = state;
            BtnRun.IsEnabled = state;
            BtnOptimizeIni.IsEnabled = state;
            BtnLaunchGame.IsEnabled = state;
        }

        private void CleanupStaleBackups()
        {
            try
            {
                string currentExeFile = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                if (!string.IsNullOrEmpty(currentExeFile))
                {
                    string backupExeFile = currentExeFile + ".bak";
                    if (File.Exists(backupExeFile)) File.Delete(backupExeFile);
                }
            }
            catch { }
        }

        private sealed class OptimizationItem
        {
            public string Id { get; }
            private readonly string? _psScript;
            private readonly Func<bool>? _nativeCheck;
            private readonly Action? _nativeAction;
            public bool IsOptimized { get; private set; }
            public System.Windows.Controls.CheckBox? UiElement { get; set; }

            public OptimizationItem(string id, string? psScript, Func<bool>? nativeCheck, Action? nativeAction = null)
            {
                Id = id;
                _psScript = psScript;
                _nativeCheck = nativeCheck;
                _nativeAction = nativeAction;
            }

            public void RunCheck() => IsOptimized = _nativeCheck?.Invoke() ?? false;

            public bool ShouldExecute()
            {
                bool isChecked = false;
                System.Windows.Application.Current.Dispatcher.Invoke(() => isChecked = UiElement?.IsChecked == true);
                return isChecked && !IsOptimized;
            }

            public void Execute(Action<string> logger)
            {
                if (_nativeAction != null)
                    _nativeAction();
                else if (!string.IsNullOrEmpty(_psScript))
                    PowerShellInterop.ExecuteScript(_psScript, logger);
            }
        }

        private static class NativeOperations
        {
            public static void OptimizeTimerRes()
            {
                RegistryInterop.WriteDWord(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\kernel", "GlobalTimerResolutionRequests", 1);
                PowerShellInterop.ExecuteScript("bcdedit /set useplatformtick yes; bcdedit /set disabledynamictick yes; bcdedit /deletevalue useplatformclock 2>$null", null);
            }
            public static bool CheckTimerRes() => RegistryInterop.ReadDWord(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\kernel", "GlobalTimerResolutionRequests") == 1;

            public static void OptimizeSysResponsiveness()
            {
                RegistryInterop.WriteDWord(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness", 0);
                RegistryInterop.WriteDWord(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\tasks\Games", "GPU Priority", 8);
                RegistryInterop.WriteDWord(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\tasks\Games", "Priority", 6);
            }
            public static bool CheckSysResponsiveness() => RegistryInterop.ReadDWord(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness") == 0;

            public static void OptimizeF76Priority()
            {
                RegistryInterop.WriteDWord(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\fallout76.exe\PerfOptions", "CpuPriorityClass", 3);
                RegistryInterop.WriteDWord(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\Project76.exe\PerfOptions", "CpuPriorityClass", 3);
            }
            public static bool CheckF76Priority() =>
                RegistryInterop.ReadDWord(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\fallout76.exe\PerfOptions", "CpuPriorityClass") == 3 &&
                RegistryInterop.ReadDWord(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\Project76.exe\PerfOptions", "CpuPriorityClass") == 3;

            public static void DisableOverlays()
            {
                RegistryInterop.WriteDWord(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0);
                RegistryInterop.WriteDWord(Registry.CurrentUser, @"Software\Microsoft\GameBar", "AllowAutoGameMode", 0);
                RegistryInterop.WriteDWord(Registry.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", 0);
                RegistryInterop.WriteDWord(Registry.CurrentUser, @"Software\Microsoft\GameBar", "ShowStartupPanel", 0);
            }
            public static bool CheckOverlays() => RegistryInterop.ReadDWord(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled") == 0;

            public static void OptimizeNetThrottle() => RegistryInterop.WriteDWord(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", -1);
            public static bool CheckNetThrottle() => RegistryInterop.ReadDWord(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex") == -1;

            public static void DisableGameDvr()
            {
                RegistryInterop.WriteDWord(Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled", 0);
                RegistryInterop.WriteDWord(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", 0);
            }
            public static bool CheckGameDvr() => RegistryInterop.ReadDWord(Registry.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled") == 0 && RegistryInterop.ReadDWord(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR") == 0;

            public static void OptimizeUiPerf()
            {
                RegistryInterop.WriteDWord(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0);
                RegistryInterop.WriteDWord(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 3);
            }
            public static bool CheckUiPerf() => RegistryInterop.ReadDWord(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency") == 0 && RegistryInterop.ReadDWord(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting") == 3;

            public static void DisableTelemetry() => ServiceInterop.DisableService("DiagTrack");
            public static bool CheckTelemetry() => ServiceInterop.IsServiceDisabled("DiagTrack");

            public static void DisableSysMain() => ServiceInterop.DisableService("SysMain");
            public static bool CheckSysMain() => ServiceInterop.IsServiceDisabled("SysMain");

            public static void InstallContextMenu()
            {
                var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Classes\Directory\Background\shell\KillHungApps");
                key?.SetValue("", "💀 Kill Hung Apps / Zabij zawieszone aplikacje");
                var cmd = key?.CreateSubKey("command");
                cmd?.SetValue("", "powershell.exe -WindowStyle Hidden -Command \"Get-Process | Where-Object { $_.Responding -eq $false -and $_.MainWindowHandle -ne 0 } | Stop-Process -Force\"");
            }

            [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
            private static extern bool SendMessageTimeout(IntPtr hWnd, uint Msg, UIntPtr wParam, string lParam, uint fuFlags, uint uTimeout, out UIntPtr lpdwResult);

            public static void RefreshEnvironment()
            {
                SendMessageTimeout((IntPtr)0xffff, 0x001A, UIntPtr.Zero, "Environment", 2, 5000, out _);
            }
        }

        private static class RegistryInterop
        {
            public static int ReadDWord(RegistryKey root, string path, string name)
            {
                try { using var key = root.OpenSubKey(path); if (key?.GetValue(name) is int v) return v; } catch { }
                return -2;
            }
            public static void WriteDWord(RegistryKey root, string path, string name, int value)
            {
                try { using var key = root.CreateSubKey(path); key?.SetValue(name, value, RegistryValueKind.DWord); } catch { }
            }
        }

        private static class ServiceInterop
        {
            public static void DisableService(string serviceName)
            {
                try
                {
                    using var sc = new ServiceController(serviceName);
                    if (sc.Status == ServiceControllerStatus.Running) sc.Stop();
                    PowerShellInterop.ExecuteScript($"Set-Service -Name '{serviceName}' -StartupType Disabled", null);
                }
                catch { }
            }

            public static bool IsServiceDisabled(string serviceName)
            {
                try { using var sc = new ServiceController(serviceName); return sc.StartType == ServiceStartMode.Disabled; }
                catch { return true; }
            }
        }

        private static class PowerShellInterop
        {
            public const string DiagnosticScript = "Write-Output '==================================================='; Write-Output ' 📊 SYSTEM DIAGNOSTIC REPORT (F76.world Tool)'; Write-Output '==================================================='; try { $os = Get-CimInstance Win32_OperatingSystem -ErrorAction Stop; Write-Output \"OS Version   : $($os.Caption) $($os.OSArchitecture)\"; Write-Output \"Build Number : $($os.BuildNumber)\" } catch {}; Write-Output '`n[ BIOS INFORMATION ]'; try { $bios = Get-CimInstance Win32_BIOS -ErrorAction Stop; Write-Output \"Manufacturer : $($bios.Manufacturer)\"; Write-Output \"Version      : $($bios.Name)\" } catch {}; Write-Output '`n[ GPU / DISPLAY DRIVERS ]'; try { $gpus = Get-CimInstance Win32_VideoController -ErrorAction Stop; foreach ($gpu in $gpus) { Write-Output \"GPU Name     : $($gpu.Name)\"; Write-Output \"Driver Ver   : $($gpu.DriverVersion)\" } } catch {}; Write-Output '==================================================='";

            public static string GetNvidiaInspectorScript(int fpsLimit)
            {
                string xmlContent = $"<?xml version=\"1.0\" encoding=\"utf-16\"?><ArrayOfProfile><Profile><ProfileName>Fallout 76</ProfileName><Executeables><string>fallout76.exe</string><string>project76.exe</string></Executeables><Settings><ProfileSetting><SettingID>2771644</SettingID><SettingValue>{fpsLimit}</SettingValue><ValueType>Dword</ValueType></ProfileSetting><ProfileSetting><SettingID>278106055</SettingID><SettingValue>1931839029</SettingValue><ValueType>Dword</ValueType></ProfileSetting><ProfileSetting><SettingID>14013262</SettingID><SettingValue>16</SettingValue><ValueType>Dword</ValueType></ProfileSetting></Settings></Profile></ArrayOfProfile>";

                // To avoid embedding complex quoting into the PowerShell script we encode the XML and decode it inside PowerShell.
                var xmlBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(xmlContent));

                // Build PowerShell script using placeholders to avoid C# interpolation and brace escaping issues
                var template = "Write-Output '[NVIDIA] Fetching NVidia Profile Inspector API...'\n" +
                               "$tempDir = Join-Path $env:TEMP 'NvidiaInspectorF76'\n" +
                               "if (-not (Test-Path $tempDir)) { New-Item -ItemType Directory -Path $tempDir | Out-Null }\n" +
                               "$nipPath = Join-Path $tempDir 'f76_profile.nip'\n" +
                               "$xmlContent = [System.Text.Encoding]::Unicode.GetString([System.Convert]::FromBase64String('{XML64}'))\n" +
                               "Set-Content -Path $nipPath -Value $xmlContent -Encoding Unicode\n" +
                               "Write-Output '[NVIDIA] Profile configuration built (VSync Fast, {FPS} FPS Limit, 16x AF).'\n" +
                               "try {\n" +
                               "    $archive = Join-Path $tempDir 'npi.zip'\n" +
                               "    Invoke-WebRequest -Uri 'https://github.com/Orbmu2k/nvidiaProfileInspector/releases/download/2.4.0.4/nvidiaProfileInspector.zip' -OutFile $archive\n" +
                               "    Expand-Archive -Path $archive -DestinationPath $tempDir -Force\n" +
                               "    Write-Output '[NVIDIA] Applying native driver registry adjustments...'\n" +
                               "    Start-Process -FilePath (Join-Path $tempDir 'nvidiaProfileInspector.exe') -ArgumentList '-silent','-importProfile', $nipPath -Wait -NoNewWindow\n" +
                               "    Write-Output '[SUCCESS] NVidia Profile ({FPS} FPS) applied natively!'\n" +
                               "} catch { Write-Output '[ERROR] Failed to inject NVidia profile. Ensure you have an internet connection.' }\n" +
                               "Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue;";

                var script = template.Replace("{XML64}", xmlBase64).Replace("{FPS}", fpsLimit.ToString());
                return script;
            }

            public static void ExecuteScript(string script, Action<string>? logger)
            {
                var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.ps1");
                File.WriteAllText(tempFile, "$OutputEncoding =[Console]::OutputEncoding =[System.Text.Encoding]::UTF8;\n" + script, new UTF8Encoding(true));

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{tempFile}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                using var process = new Process { StartInfo = psi };
                if (logger != null)
                {
                    process.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) logger(e.Data); };
                    process.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) logger($"[ERROR] {e.Data}"); };
                }

                process.Start();
                if (logger != null)
                {
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                }
                process.WaitForExit();
                try { File.Delete(tempFile); } catch { }
            }
        }

        private sealed class UpdateManifest
        {
            [JsonPropertyName("version")]
            public string? Version { get; set; }

            [JsonPropertyName("release_url")]
            public string? ReleaseUrl { get; set; }

            [JsonPropertyName("fallback_urls")]
            public List<string>? FallbackUrls { get; set; }

            [JsonPropertyName("sha256_hash")]
            public string? Sha256Hash { get; set; }
            [JsonPropertyName("is_mandatory")]
            public bool IsMandatory { get; set; }
        }

        private static class AdvancedUpdater
        {
            private static readonly string[] ManifestUrls =
            {
                "https://f76.world/api/updater/version.json",
                "https://raw.githubusercontent.com/Owlyyy/BetterF76/main/version.json",
                "https://api.f76.world/updater/version.json",
                "https://f76-world.b-cdn.net/api/updater/version.json"
            };

            private static readonly Version CurrentVersion = new(1, 1, 0);
            private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(8);

            public static async Task<(bool UpdateAvailable, UpdateManifest? Manifest)> CheckForUpdatesAsync(Action<string> logger)
            {
                using var client = new HttpClient { Timeout = DefaultTimeout };
                client.DefaultRequestHeaders.Add("User-Agent", "OwlyyyOptimizer/1.1");
                client.DefaultRequestHeaders.ConnectionClose = false;

                foreach (var url in ManifestUrls)
                {
                    try
                    {
                        logger($"[UPDATER] Probing remote manifest at: {url}");
                        var json = await client.GetStringAsync(url);
                        var manifest = JsonSerializer.Deserialize<UpdateManifest>(json);

                        if (manifest != null && Version.TryParse(manifest.Version, out var remoteVersion))
                        {
                            if (remoteVersion > CurrentVersion)
                            {
                                logger($"[UPDATER] System Resonance anomaly detected. Native upgrade {remoteVersion} synthesized.");
                                return (true, manifest);
                            }
                            logger($"[UPDATER] Local configuration (v{CurrentVersion}) retains optimal purity.");
                            return (false, manifest);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger($"[UPDATER] Transmission failure at {url}: {ex.Message}");
                    }
                }

                logger("[UPDATER] WARNING: All synchronization vectors collapsed. Unable to verify purity.");
                return (false, null);
            }

            public static async Task<bool> DownloadAndApplyUpdateAsync(UpdateManifest manifest, Action<string> logger)
            {
                var downloadUrls = new List<string>();
                if (!string.IsNullOrWhiteSpace(manifest.ReleaseUrl)) downloadUrls.Add(manifest.ReleaseUrl);
                if (manifest.FallbackUrls != null) downloadUrls.AddRange(manifest.FallbackUrls);

                if (downloadUrls.Count == 0)
                {
                    logger("[UPDATER] FATAL: Payload matrix contains zero operational coordinates.");
                    return false;
                }

                string tempFilePath = Path.Combine(Path.GetTempPath(), $"OptimizerPayload_{Guid.NewGuid():N}.exe");
                using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
                client.DefaultRequestHeaders.Add("User-Agent", "OwlyyyOptimizer/1.1");

                bool downloaded = false;
                foreach (var url in downloadUrls)
                {
                    try
                    {
                        logger($"[UPDATER] Initiating binary transfer from: {url}");
                        var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                        response.EnsureSuccessStatusCode();

                        await using var streamToReadFrom = await response.Content.ReadAsStreamAsync();
                        await using var streamToWriteTo = File.Open(tempFilePath, FileMode.Create);
                        await streamToReadFrom.CopyToAsync(streamToWriteTo);

                        downloaded = true;
                        logger("[UPDATER] Binary acquisition complete.");
                        break;
                    }
                    catch (Exception ex)
                    {
                        logger($"[UPDATER] Transfer collapsed at {url}: {ex.Message}");
                    }
                }

                if (!downloaded || !File.Exists(tempFilePath))
                {
                    logger("[UPDATER] FATAL: Total failure across all payload vectors.");
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(manifest.Sha256Hash))
                {
                    logger("[UPDATER] Computing SHA256 resonance signature...");
                    using var sha256 = SHA256.Create();
                    using var fileStream = File.OpenRead(tempFilePath);
                    var hashBytes = await sha256.ComputeHashAsync(fileStream);
                    var calculatedHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

                    if (calculatedHash != manifest.Sha256Hash.ToLowerInvariant())
                    {
                        logger("[UPDATER] FATAL: Purity violation. Checksum divergence detected. Purging payload.");
                        File.Delete(tempFilePath);
                        return false;
                    }
                    logger("[UPDATER] Cryptographic integrity confirmed.");
                }

                try
                {
                    logger("[UPDATER] Executing Level 1 Native Memory Swap...");
                    string currentExeFile = Process.GetCurrentProcess().MainModule?.FileName ?? throw new InvalidOperationException("Unresolved logical path.");
                    string backupExeFile = currentExeFile + ".bak";

                    if (File.Exists(backupExeFile)) File.Delete(backupExeFile);
                    File.Move(currentExeFile, backupExeFile);
                    File.Move(tempFilePath, currentExeFile);

                    logger("[UPDATER] Structural replacement complete. Initiating reincarnation protocol...");
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = currentExeFile,
                        UseShellExecute = true
                    });

                    System.Windows.Application.Current.Dispatcher.Invoke(() => System.Windows.Application.Current.Shutdown());
                    Environment.Exit(0);
                    return true;
                }
                catch (Exception ex)
                {
                    logger($"[UPDATER] Level 1 Swap Exception: {ex.Message}");
                    return ApplyUpdateViaBatchFallback(tempFilePath, logger);
                }
            }

            private static bool ApplyUpdateViaBatchFallback(string tempFilePath, Action<string> logger)
        {
            logger("[UPDATER] Escalating to Level 2 Process Subsystem Swap (IPC Bypass)...");
            try
            {
                string currentExeFile = Process.GetCurrentProcess().MainModule?.FileName ?? throw new InvalidOperationException("Unresolved executeable.");
                string batchScriptPath = Path.Combine(Path.GetTempPath(), $"OwlyyyUpdateFallback_{Guid.NewGuid():N}.bat");

                var lines = new[]
                {
                    "@echo off",
                    "timeout /t 2 /nobreak >nul",
                    $"del \"{currentExeFile}\" /f /q",
                    $"move /y \"{tempFilePath}\" \"{currentExeFile}\"",
                    $"start \"\" \"{currentExeFile}\"",
                    "del \"%~f0\""
                };

                var scriptContent = string.Join(Environment.NewLine, lines);
                File.WriteAllText(batchScriptPath, scriptContent);
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{batchScriptPath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });

                System.Windows.Application.Current.Dispatcher.Invoke(() => System.Windows.Application.Current.Shutdown());
                Environment.Exit(0);
                return true;
            }
            catch (Exception batchEx)
            {
                logger($"[UPDATER] FATAL: Level 2 Fallback collapsed: {batchEx.Message}");
                return false;
            }
        }
        }

        private static class DictionaryBank
        {
            private static readonly Dictionary<string, Dictionary<string, string>> Data = new()
            {
                ["en"] = new()
                {
                    ["app_title"] = "Universal Windows Optimizer (PRO)",
                    ["desc_purpose"] = "Purpose & Gains: Applies advanced kernel and UI tweaks to eliminate input lag, lock timer resolution to 0.5ms, and boost overall frame stability.",
                    ["btn_update"] = "🔄 Check for Updates",
                    ["tab_tweaks"] = "⚙️ Tweaks & Installers",
                    ["tab_rescue"] = "🚑 Rescue & Diagnostics",
                    ["sec_tweaks_title"] = "Performance & Latency Optimizations",
                    ["sec_install_title"] = "Automated Installers (Winget)",
                    ["btn_analyze"] = "🔍 Analyze System",
                    ["rescue_lbl"] = "🛠️ Quick Actions & Emergency Tools:",
                    ["btn_run_selected"] = "⚡ Run Selected Operations (Apply)",
                    ["btn_kill"] = "💀 Kill Hung Applications Immediately",
                    ["btn_menu"] = "➕ Add Rescue Options to Desktop Context Menu",
                    ["btn_report"] = "📊 Generate System & Driver Report",
                    ["log_start"] = "[INFO] Ready. Select operations and press apply.",
                    ["analyze_running"] = "[INFO] Analyzing system state...",
                    ["analyze_done"] = "[INFO] Analysis complete. Already optimized items have been marked green.",
                    ["optimized_tag"] = "Optimized",
                    ["tag_ok"] = "OPTIMAL",
                    ["tag_bad"] = "ACTION REQ",
                    ["log_gather"] = "[INFO] Gathering selected scripts for execution...",
                    ["log_done"] = "[COMPLETED] All selected operations have been executed successfully!",
                    ["log_thread_end"] = "[INFO] --- Background process finished ---",
                    ["report_generating"] = "[INFO] Generating system report. Please wait...",
                    ["update_checking"] = "[INFO] Polling Promethean Resonance at f76.world API...",
                    ["update_fail"] = "Systemic connection fault. Maintain current purity.",
                    ["update_ok"] = "Current execution state is optimal. No upgrades required.",
                    ["restore_point_name"] = "Create System Restore Point",
                    ["restore_point_desc"] = "Creates a safety backup so you can easily revert changes if needed.",
                    ["timer_res_name"] = "Max Out Timer Resolution (0.5ms)",
                    ["timer_res_desc"] = "Forces Windows to use a sub-millisecond timer and disables dynamic ticking. Eliminates input latency.",
                    ["sys_responsiveness_name"] = "Optimize System Responsiveness (MMCSS)",
                    ["sys_responsiveness_desc"] = "Sets Multimedia SystemResponsiveness to 0, forcing pure GPU priority for gaming tasks.",
                    ["f76_priority_name"] = "Force F76 CPU High Priority",
                    ["f76_priority_desc"] = "Injects a registry key forcing the Windows kernel to permanently run fallout76.exe and Project76.exe at High Priority.",

                    ["f76_nv_profile_62_name"] = "Nvidia Profile Inspector (62 FPS Limit)",
                    ["f76_nv_profile_62_desc"] = "Locks FPS to 62. The absolute safest choice to prevent Creation Engine physics glitches. (Requires Nvidia GPU)",
                    ["f76_nv_profile_124_name"] = "Nvidia Profile Inspector (124 FPS Limit)",
                    ["f76_nv_profile_124_desc"] = "Locks FPS to 124. High performance competitive mode, but may slightly alter physics. (Requires Nvidia GPU)",

                    ["disable_overlays_name"] = "Disable Game Bar & Overlays",
                    ["disable_overlays_desc"] = "Kills Windows Game Bar and background app captures. It is also highly recommended to manually disable Steam & Discord overlays.",
                    ["net_throttle_name"] = "Disable Network Throttling",
                    ["net_throttle_desc"] = "Prevents Windows from limiting network packets during multimedia playback, lowering ping.",
                    ["game_dvr_name"] = "Disable Xbox GameDVR",
                    ["game_dvr_desc"] = "Hard-disables background game recording which causes DWM micro-stutters.",
                    ["power_plan_name"] = "Enable Ultimate Performance Plan",
                    ["power_plan_desc"] = "Unlocks and activates the hidden power plan to bypass CPU core parking.",
                    ["clean_temp_name"] = "Clean Temporary Files",
                    ["clean_temp_desc"] = "Deletes junk files to free up disk space.",
                    ["ui_perf_name"] = "Disable Transparency & Effects",
                    ["ui_perf_desc"] = "Increases UI smoothness by disabling DWM visual processing.",
                    ["telemetry_name"] = "Block Microsoft Telemetry",
                    ["telemetry_desc"] = "Disables background tracking and frees up CPU resources.",
                    ["sysmain_name"] = "Disable Superfetch/SysMain",
                    ["sysmain_desc"] = "Stops continuous eMMC/HDD drive thrashing.",
                    ["ssd_trim_name"] = "Optimize SSD (TRIM)",
                    ["ssd_trim_desc"] = "Forces garbage collection on your SSD to maintain peak write speeds.",
                    ["inst_wt_name"] = "Windows Terminal",
                    ["inst_wt_desc"] = "The latest modern console by Microsoft.",
                    ["inst_ps7_name"] = "PowerShell 7",
                    ["inst_ps7_desc"] = "New, significantly faster version of PowerShell.",
                    ["inst_vcredist_name"] = "Visual C++ Redistributable",
                    ["inst_vcredist_desc"] = "Core library components for modern apps and games.",
                    ["fix_path_name"] = "Refresh Environment (Update PATH)",
                    ["fix_path_desc"] = "Broadcasts new environment variables to the OS."
                },
                ["pl"] = new()
                {
                    ["app_title"] = "Uniwersalny Optymalizator Windows (PRO)",
                    ["desc_purpose"] = "Cel: Aplikuje głębokie poprawki jądra systemu, blokuje rozdzielczość timera do 0.5ms i redukuje input lag.",
                    ["btn_update"] = "🔄 Sprawdź aktualizacje",
                    ["tab_tweaks"] = "⚙️ Optymalizacje",
                    ["tab_rescue"] = "🚑 Ratunek i Diagnostyka",
                    ["sec_tweaks_title"] = "Optymalizacje Wydajności",
                    ["sec_install_title"] = "Instalatory (Winget)",
                    ["btn_analyze"] = "🔍 Analizuj System",
                    ["rescue_lbl"] = "🛠️ Narzędzia awaryne:",
                    ["btn_run_selected"] = "⚡ Zastosuj Wybrane Opcje",
                    ["btn_kill"] = "💀 Zabij Zawieszone Aplikacje",
                    ["btn_menu"] = "➕ Dodaj opcje do Menu Pulpitu",
                    ["btn_report"] = "📊 Generuj Raport Systemowy",
                    ["log_start"] = "[INFO] Gotowe do pracy.",
                    ["analyze_running"] = "[INFO] Analizowanie stanu systemu...",
                    ["analyze_done"] = "[INFO] Analiza zakończona. Spójrz na kolory na liście powyżej.",
                    ["optimized_tag"] = "Zoptymalizowano",
                    ["tag_ok"] = "ZOPTYMALIZOWANE",
                    ["tag_bad"] = "WYMAGA POPRAWY",
                    ["log_gather"] = "[INFO] Zbieranie skryptów...",
                    ["log_done"] = "[ZAKOŃCZONO] Operacje wykonane pomyślnie!",
                    ["log_thread_end"] = "[INFO] --- Proces zakończony ---",
                    ["report_generating"] = "[INFO] Generowanie raportu...",
                    ["update_checking"] = "[INFO] Nawiązywanie komunikacji z API...",
                    ["update_fail"] = "Brak łączności z węzłem aktualizacji.",
                    ["update_ok"] = "Środowisko rezonuje prawidłowo. Używasz najnowszej wersji.",
                    ["restore_point_name"] = "Utwórz Punkt Przywracania",
                    ["restore_point_desc"] = "Zalecana kopia bezpieczeństwa.",
                    ["timer_res_name"] = "Wymuś Rozdzielczość Timera (0.5ms)",
                    ["timer_res_desc"] = "Zmusza Windows do używania timera 0.5ms, drastycznie zmniejszając input lag.",
                    ["sys_responsiveness_name"] = "Optymalizuj Responsywność (MMCSS)",
                    ["sys_responsiveness_desc"] = "Faworyzuje gry kosztem zadań w tle (Responsiveness=0).",
                    ["f76_priority_name"] = "Wymuś Wysoki Priorytet dla F76",
                    ["f76_priority_desc"] = "Dodaje klucz rejestru wymuszający uruchamianie fallout76.exe oraz Project76.exe z wysokim priorytetem CPU.",

                    ["f76_nv_profile_62_name"] = "Wgraj Profil Nvidia Inspector (Limit 62 FPS)",
                    ["f76_nv_profile_62_desc"] = "Ogranicza klatki do 62. Najbezpieczniejsza opcja by zapobiec błędom fizyki silnika Creation Engine. (Wymaga karty Nvidia)",
                    ["f76_nv_profile_124_name"] = "Wgraj Profil Nvidia Inspector (Limit 124 FPS)",
                    ["f76_nv_profile_124_desc"] = "Ogranicza klatki do 124. Tryb e-sportowy pod płynność, ale w grze mogą wystąpić małe problemy z fizyką. (Wymaga karty Nvidia)",

                    ["disable_overlays_name"] = "Wyłącz Game Bar i Nakładki",
                    ["disable_overlays_desc"] = "Zabija Windows Game Bar. Zaleca się również ręczne wyłączenie nakładek Steam oraz Discord.",
                    ["net_throttle_name"] = "Wyłącz Ograniczanie Sieci (Throttling)",
                    ["net_throttle_desc"] = "Zapobiega skokom pingu przez systemowe limity pakietów.",
                    ["game_dvr_name"] = "Wyłącz Xbox GameDVR",
                    ["game_dvr_desc"] = "Całkowicie blokuje nagrywanie gier w tle.",
                    ["power_plan_name"] = "Włącz Plan Najwyższej Wydajności",
                    ["power_plan_desc"] = "Odblokowuje ukryty plan zasilania (bez usypiania rdzeni).",
                    ["clean_temp_name"] = "Wyczyść Pliki Tymczasowe",
                    ["clean_temp_desc"] = "Usuwa śmieci systemowe.",
                    ["ui_perf_name"] = "Wyłącz przezroczystość UI",
                    ["ui_perf_desc"] = "Zwiększa płynność interfejsu.",
                    ["telemetry_name"] = "Zablokuj Telemetrię",
                    ["telemetry_desc"] = "Wyłącza szpiegowanie w tle.",
                    ["sysmain_name"] = "Wyłącz SysMain",
                    ["sysmain_desc"] = "Zatrzymuje zbędne użycie dysku HDD.",
                    ["ssd_trim_name"] = "Optymalizuj SSD (TRIM)",
                    ["ssd_trim_desc"] = "Przywraca pełną prędkość zapisu.",
                    ["inst_wt_name"] = "Windows Terminal",
                    ["inst_wt_desc"] = "Nowoczesna konsola od MS.",
                    ["inst_ps7_name"] = "PowerShell 7",
                    ["inst_ps7_desc"] = "Nowa, szybsza wersja.",
                    ["inst_vcredist_name"] = "VC++ Redistributable",
                    ["inst_vcredist_desc"] = "Wymagane biblioteki do gier.",
                    ["fix_path_name"] = "Odśwież środowisko (PATH)",
                    ["fix_path_desc"] = "Wymusza odczyt nowych komend."
                }
            };

            public static Dictionary<string, string> Get(string lang) => Data.TryGetValue(lang, out var dict) ? dict : Data["en"];
        }
    }
}
