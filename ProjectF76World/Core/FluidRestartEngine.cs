using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace ProjectF76World.Core;

/// <summary>
/// Silnik odpowiedzialny za asynchroniczną weryfikację i bezstratne wdrażanie aktualizacji
/// aplikacji w strukturze Single-File Executable poprzez zewnętrzny skrypt powłoki.
/// </summary>
public sealed class FluidRestartEngine
{
    private readonly HttpClient _httpClient;
    private const string VersionUrl = "https://f76.world/api/launcher/version";
    private const string DownloadUrl = "https://f76.world/api/launcher/download";

    // Bieżąca wersja zakodowana w kodzie źródłowym klienta
    public static readonly Version CurrentVersion = new(1, 0, 0);

    public FluidRestartEngine()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("BetterF76-Launcher/1.0");
    }

    /// <summary>
    /// Sprawdza, czy na serwerze f76.world znajduje się nowsza wersja aplikacji.
    /// </summary>
    public async Task<bool> CheckForUpdatesAsync()
    {
        try
        {
            string versionString = await _httpClient.GetStringAsync(VersionUrl).ConfigureAwait(false);
            if (Version.TryParse(versionString.Trim(), out Version? remoteVersion))
            {
                return remoteVersion > CurrentVersion;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UpdateEngine] Nie udało się sprawdzić aktualizacji: {ex.Message}");
        }
        return false;
    }

    /// <summary>
    /// Pobiera nowy plik wykonywalny i inicjuje procedurę wymiany binarnej w systemie operacyjnym.
    /// </summary>
    public async Task<bool> ExecuteUpdateAsync()
    {
        try
        {
            string currentExePath = Environment.ProcessPath ?? Environment.GetCommandLineArgs()[0];
            string? currentDirectory = Path.GetDirectoryName(currentExePath);

            if (string.IsNullOrEmpty(currentDirectory)) return false;

            string pendingUpdatePath = Path.Combine(currentDirectory, "BetterF76_Update.tmp");

            // Pobieranie nowego pliku binarnego
            byte[] fileBytes = await _httpClient.GetByteArrayAsync(DownloadUrl).ConfigureAwait(false);
            await File.WriteAllBytesAsync(pendingUpdatePath, fileBytes).ConfigureAwait(false);

            if (OperatingSystem.IsWindows())
            {
                ExecuteWindowsUpdater(currentExePath, pendingUpdatePath);
            }
            else
            {
                ExecuteLinuxUpdater(currentExePath, pendingUpdatePath);
            }

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UpdateEngine] Krytyczny błąd podczas aktualizacji: {ex.Message}");
            return false;
        }
    }

    private static void ExecuteWindowsUpdater(string currentExePath, string pendingUpdatePath)
    {
        string batchPath = Path.Combine(Path.GetDirectoryName(currentExePath)!, "updater.bat");

        // Tworzenie bezblokadowego skryptu wsadowego CMD, który poczeka na zamknięcie głównego procesu
        string batchScript = $"""
        @echo off
        :retry
        timeout /t 1 /nobreak > nul
        move /y "{pendingUpdatePath}" "{currentExePath}" > nul 2>&1
        if exist "{pendingUpdatePath}" goto retry
        start "" "{currentExePath}"
        del "%~f0"
        """;

        File.WriteAllText(batchPath, batchScript);

        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{batchPath}\"",
            CreateNoWindow = true,
            UseShellExecute = false
        });

        Environment.Exit(0);
    }

    private static void ExecuteLinuxUpdater(string currentExePath, string pendingUpdatePath)
    {
        string scriptPath = Path.Combine(Path.GetDirectoryName(currentExePath)!, "updater.sh");

        // Skrypt bash dla środowiska Linux (np. Proton/Natywne) realizujący atomowe zastąpienie pliku
        string bashScript = $"""
        #!/bin/bash
        sleep 1
        mv -f "{pendingUpdatePath}" "{currentExePath}"
        chmod +x "{currentExePath}"
        "{currentExePath}" &
        rm -- "$0"
        """;

        File.WriteAllText(scriptPath, bashScript);

        // Nadanie praw wykonywalności dla skryptu aktualizacyjnego przez bash
        Process.Start(new ProcessStartInfo
        {
            FileName = "chmod",
            Arguments = $"+x \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        })?.WaitForExit();

        Process.Start(new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"\"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });

        Environment.Exit(0);
    }
}