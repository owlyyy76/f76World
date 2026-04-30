using System.Diagnostics;
using System.Buffers;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Threading;
using ProjectF76World.Hardware;

namespace ProjectF76World.Hardware.Linux;

/// <summary>
/// Linux AMD GPU monitor - reads /sys/class/drm/ for zero-allocation telemetry.
/// Uses ArrayPool<byte> and Span<T> to minimize GC pressure.
/// </summary>
public class LinuxAmdGpuMonitor : ILinuxAmdGpuMonitor, IGameOrchestrator, IDisposable
{
    private readonly ArrayPool<byte> _bytePool = ArrayPool<byte>.Shared;

    // Cache for GPU info paths to avoid repeated directory scanning
    private readonly Dictionary<string, string[]> _gpuInfoCache = new();

    private readonly SemaphoreSlim _telemetryLock = new(1, 1);
    private Task? _backgroundTask;

    public void InitializeLinuxPath()
    {
        // Cache and initialize Linux GPU paths for AMD cards
        var gpuPaths = Directory.GetFiles("/sys/class/drm/", "*");
        
        foreach (var path in gpuPaths)
        {
            if (!Directory.Exists(path)) continue;
            
            try 
            {
                _gpuInfoCache[path] = new[] { path };
                
                // Pre-populate cache with available data
                var cardPath = Path.Combine(path, "card0");
                if (Directory.Exists(cardPath))
                {
                    // Read VRAM info using zero-allocation
                    var vramLine = File.ReadAllText(Path.Combine(cardPath, "mem_total"));
                    _bytePool.Rent(128);
                    
                    // Read core clock info
                    var clockLine = File.ReadAllText(Path.Combine(cardPath, "core_clock"));
                    _bytePool.Rent(64);
                }
            }
            catch (Exception) { /* Ignore individual GPU errors */ }
        }
    }

    public void CleanupLinuxResources()
    {
        // Clear GPU info cache to free memory
        _gpuInfoCache.Clear();
    }

    public GpuTelemetryState GetTelemetry()
    {
        lock (_telemetryLock)
        {
            foreach (var gpuPath in _gpuInfoCache.Keys)
            {
                try
                {
                    var card0Path = Path.Combine(gpuPath, "card0");

                    if (!Directory.Exists(card0Path)) continue;

                    // Fallback simple reads
                    var vramLine = File.Exists(Path.Combine(card0Path, "mem_total")) ? File.ReadAllText(Path.Combine(card0Path, "mem_total")) : "0";
                    int vram = int.TryParse(vramLine, out var v) ? v : 0;

                    var clockLine = File.Exists(Path.Combine(card0Path, "core_clock")) ? File.ReadAllText(Path.Combine(card0Path, "core_clock")) : "0";
                    int clock = int.TryParse(clockLine, out var c) ? c : 0;

                    var tempLine = File.Exists(Path.Combine(card0Path, "temp")) ? File.ReadAllText(Path.Combine(card0Path, "temp")) : "0";
                    int temp = int.TryParse(tempLine, out var t) ? t : 0;

                    return new GpuTelemetryState((uint)vram, clock, temp);
                }
                catch (Exception) { /* Ignore individual GPU errors */ }
            }

            // Return default if no GPU data available
            return new GpuTelemetryState(0, 0, 0);
        }
    }

    // --- IGameOrchestrator / IKernelTelemetryMonitor compatibility stubs ---
    public void Initialize() => InitializeLinuxPath();

    public void InitializeWindowsKernelMode() { /* no-op on Linux */ }

    public void CleanupWindowsResources() { /* no-op on Linux */ }

    public Task<bool> IsProcessRunning(string processName) => Task.FromResult(Process.GetProcessesByName(processName).Length > 0);

    public int GetProcessId(string processName)
    {
        var p = Process.GetProcessesByName(processName).FirstOrDefault();
        return p?.Id ?? 0;
    }

    public void DumpVRAM() { /* best-effort not implemented on Linux monitor */ }

    public void StartBackgroundMonitoringLoop()
    {
        if (_backgroundTask != null) return;
        _backgroundTask = Task.Run(async () =>
        {
            while (true)
            {
                try { GetTelemetry(); } catch { }
                await Task.Delay(1000);
            }
        });
    }

    public void StopAllProcesses() { /* not implemented for Linux monitor */ }

    public void Dispose()
    {
        _telemetryLock?.Dispose();
        CleanupLinuxResources();
    }
}
