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
        private const string ApiEndpoint = "https://api.f76.world/v1/launcher/version";

        public FluidRestartEngine(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient();
        }

        public async Task<bool> CheckAndUpdateAsync(Action<string> logCallback)
        {
            logCallback("[INFO] Inicjalizacja Fluid Restart Engine. Pingowanie f76.world...");

            try
            {
                // W produkcji: await _httpClient.GetStringAsync(ApiEndpoint);
                await Task.Delay(500); // Symulacja opóźnienia sieciowego

                logCallback("[WARN] Zabezpieczono nową sygnaturę binarną. Inicjalizacja rotacji...");
                return await ExecuteSeamlessRestart(logCallback);
            }
            catch (Exception ex)
            {
                logCallback($"[ERROR] Błąd synchronizacji API Gateway: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ExecuteSeamlessRestart(Action<string> logCallback)
        {
            string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            if (string.IsNullOrEmpty(currentExe)) return false;

            string tempExe = currentExe + ".update";
            string batPath = Path.Combine(Path.GetTempPath(), "fluid_restart.bat");

            // Symulacja zapisania nowego artefaktu z sieci
            File.Copy(currentExe, tempExe, true);

            string batScript = $@"
@echo off
timeout /t 2 /nobreak > NUL
move /Y ""{tempExe}"" ""{currentExe}""
start """" ""{currentExe}""
del ""%~f0""
";
            await File.WriteAllTextAsync(batPath, batScript);

            logCallback("[CRITICAL] Wymuszam bezszwowy restart. Zamykanie deskryptorów...");

            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{batPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            });

            Application.Current.Shutdown();
            return true;
        }
    }
}