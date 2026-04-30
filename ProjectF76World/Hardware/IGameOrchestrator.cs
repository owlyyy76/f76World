namespace ProjectF76World.Hardware;

/// <summary>
/// Game orchestrator interface - manages processes, VRAM dumping, and telemetry.
/// Cross-platform implementation using OS-specific APIs.
/// </summary>
public interface IGameOrchestrator : IDisposable
{
    /// <summary>
    /// Initialize the orchestrator with OS-specific paths.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Start background monitoring loop for telemetry updates.
    /// </summary>
    void StartBackgroundMonitoringLoop();

    /// <summary>
    /// Stop all game processes (Linux: SIGSTOP/SIGCONT on steamwebhelper).
    /// </summary>
    void StopAllProcesses();

    /// <summary>
    /// Check if a process is running using OS-specific APIs.
    /// </summary>
    Task<bool> IsProcessRunning(string processName);

    /// <summary>
    /// Get the process ID for a given process name.
    /// </summary>
    int GetProcessId(string processName);

    /// <summary>
    /// Dump VRAM by controlling steamwebhelper on Linux or equivalent on Windows.
    /// </summary>
    void DumpVRAM();
}
