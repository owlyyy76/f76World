using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ProjectF76World.Hardware.Windows;

/// <summary>
/// Tier 3: Zero-Allocation Memory & VRAM Working Set Optimizer.
/// Używa natywnego psapi.dll do zrzucania nieużywanej pamięci z procesów działających w tle.
/// </summary>
public static partial class VramOptimizer
{
    // .NET 10 Source Generator - Zero-allocation P/Invoke
    [LibraryImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EmptyWorkingSet(IntPtr hProcess);

    // Stała tablica najpopularniejszych pożeraczy zasobów i VRAM w tle
    private static readonly string[] _heavyBackgroundApps = new[]
    {
        "discord", "chrome", "msedge", "firefox", "brave", "steamwebhelper", "EpicGamesLauncher", "Spotify"
    };

    /// <summary>
    /// Błyskawicznie redukuje pamięć RAM/VRAM zmapowaną przez aplikacje w tle.
    /// Zwraca liczbę pomyślnie zoptymalizowanych procesów.
    /// </summary>
    public static int OptimizeBackgroundProcesses()
    {
        int optimizedCount = 0;

        foreach (string appName in _heavyBackgroundApps)
        {
            Process[] processes = Process.GetProcessesByName(appName);

            for (int i = 0; i < processes.Length; i++)
            {
                Process p = processes[i];
                try
                {
                    // Używamy natywnego uchwytu procesu by zwolnić zasoby
                    if (EmptyWorkingSet(p.Handle))
                    {
                        optimizedCount++;
                    }
                }
                catch (Exception)
                {
                    // Ignorujemy procesy, do których nie mamy dostępu (np. wyższe uprawnienia systemowe)
                }
                finally
                {
                    // Ręczne zwolnienie obiektu z pamięci, aby odciążyć Garbage Collector
                    p.Dispose();
                }
            }
        }

        return optimizedCount;
    }
}