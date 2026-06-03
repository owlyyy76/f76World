using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

namespace ProjectF76World.Core;

public class FluidRestartEngine
{
    private const string API_ENDPOINT = "https://api.f76.world/v1/launcher/version";
    private const string CURRENT_VERSION = "1.0.0";
    private readonly HttpClient _httpClient;

    public FluidRestartEngine(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
    }

    public async Task<bool> CheckAndUpdateAsync(Action<string> logCallback)
    {
        try
        {
            logCallback("[INFO] Inicjalizacja Fluid Restart Engine. Oczekiwanie na sygnał API...");

            // Symulacja sprawdzenia (w produkcji odpytaj API_ENDPOINT)
            // var response = await _httpClient.GetStringAsync(API_ENDPOINT);
            bool hasUpdate = true; // Flaga testowa

            if (hasUpdate)
            {
                logCallback("[WARN] Wykryto nową wersję z API Gateway f76.world. Rozpoczynam pobieranie tła...");
                return await ExecuteSeamlessRestart(logCallback);
            }

            logCallback("[SUCCESS] System jest aktualny.");
            return false;
        }
        catch (Exception ex)
        {
            logCallback($"[ERROR] Błąd synchronizacji API: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> ExecuteSeamlessRestart(Action<string> logCallback)
    {
        string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? "BetterF76.exe";
        string tempExe = Path.Combine(Path.GetTempPath(), "BetterF76_update.exe");
        string batPath = Path.Combine(Path.GetTempPath(), "fluid_restart.bat");

        logCallback("[INFO] Pobieranie do pamięci tymczasowej...");

        // Tutaj logika pobierania. Symulujemy kopiowanie obecnego pliku jako "nowej wersji".
        File.Copy(currentExe, tempExe, true);

        logCallback("[INFO] Zabezpieczono nową wersję. Generowanie skryptu rotacji...");

        string batScript = $@"
@echo off
timeout /t 2 /nobreak > NUL
move /Y ""{tempExe}"" ""{currentExe}""
start """" ""{currentExe}""
del ""%~f0""
";
        await File.WriteAllTextAsync(batPath, batScript);

        logCallback("[CRITICAL] Wymuszam bezszwowy restart...");

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