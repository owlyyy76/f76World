using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

namespace ProjectF76World.Core
{
    public class FluidRestartEngine
    {
        private readonly HttpClient _httpClient;
        private const string ApiUrl = "https://api.f76.world/v1/launcher/version";

        public FluidRestartEngine(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient();
        }

        public async Task<(bool UpdateAvailable, string? NewVersion)> CheckForUpdatesAsync(Action<string> logger)
        {
            logger("[INFO] Inicjalizacja Fluid Restart Engine. Oczekiwanie na sygnał z API Gateway f76.world...");
            try
            {
                // Symulacja ping - docelowo: await _httpClient.GetStringAsync(ApiUrl);
                await Task.Delay(500);
                return (true, "1.1.0-resonance"); // Flaga wywołująca proces update'u
            }
            catch (Exception ex)
            {
                logger($"[ERROR] Krytyczny błąd synchronizacji API: {ex.Message}");
                return (false, null);
            }
        }

        public async Task<bool> ExecuteSeamlessRestartAsync(Action<string> logger)
        {
            try
            {
                string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                if (string.IsNullOrEmpty(currentExe)) return false;

                string tempExe = currentExe + ".update";
                string batPath = Path.Combine(Path.GetTempPath(), "fluid_restart.bat");

                logger("[INFO] Zabezpieczono nową wersję binarnej istoty. Generowanie skryptu rotacji procesów...");

                // Zastąpić docelowo strumieniem pobierania Http
                File.Copy(currentExe, tempExe, true);

                string batScript = $@"
@echo off
timeout /t 2 /nobreak > NUL
move /Y ""{tempExe}"" ""{currentExe}""
start """" ""{currentExe}""
del ""%~f0""
";
                await File.WriteAllTextAsync(batPath, batScript);

                logger("[CRITICAL] Wymuszam bezszwowy restart infrastruktury...");

                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{batPath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });

                System.Windows.Application.Current.Shutdown();
                return true;
            }
            catch (Exception ex)
            {
                logger($"[ERROR] Fluid Restart Engine napotkał zator pamięci: {ex.Message}");
                return false;
            }
        }
    }
}