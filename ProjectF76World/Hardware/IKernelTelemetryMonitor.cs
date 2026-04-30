using System.Diagnostics;

namespace ProjectF76World.Hardware;

/// <summary>
/// Kernel-level telemetry monitor interface - extends ITelemetryMonitor
/// with process management and kernel-specific operations.
/// </summary>
public interface IKernelTelemetryMonitor : ITelemetryMonitor, IDisposable
{
    /// <summary>
    /// Initialize Linux kernel paths (/sys/class/).
    /// </summary>
    void InitializeLinuxPath();

    /// <summary>
    /// Initialize Windows kernel mode drivers.
    /// </summary>
    void InitializeWindowsKernelMode();

    /// <summary>
    /// Cleanup Linux kernel resources and free memory caches.
    /// </summary>
    void CleanupLinuxResources();

    /// <summary>
    /// Cleanup Windows kernel resources.
    /// </summary>
    void CleanupWindowsResources();

    /// <summary>
    /// Check if a process is running using OS-specific APIs.
    /// </summary>
    Task<bool> IsProcessRunning(string processName);

    /// <summary>
    /// Get the process ID for a given process name.
    /// </summary>
    int GetProcessId(string processName);

    /// <summary>
    /// Dump VRAM by stopping/controlling steamwebhelper on Linux or equivalent on Windows.
    /// </summary>
    void DumpVRAM();
}
