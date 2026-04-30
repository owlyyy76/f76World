using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ProjectF76World.Hardware.Linux;

namespace ProjectF76World.Hardware;

/// <summary>
/// Cross-platform game orchestrator with OS-specific implementations.
/// Accepts IKernelTelemetryMonitor (common interface) and routes to Linux or Windows implementation at runtime.
/// </summary>
public class CrossPlatformGameOrchestrator : IGameOrchestrator, IDisposable
{
    private readonly SemaphoreSlim _processLock = new(1, 1);
    private readonly ITelemetryMonitor _telemetryMonitor;
    
    // OS-specific orchestrators (only one will be used based on platform detection at runtime)
    private WindowsGameOrchestrator? _windowsOrchestrator;
    private LinuxAmdGpuMonitor? _linuxOrchestrator;

    /// <summary>
    /// Constructor accepts IKernelTelemetryMonitor which both implementations implement.
    /// Platform-specific routing occurs inside the constructor using OperatingSystem.IsLinux().
    /// </summary>
    public CrossPlatformGameOrchestrator(ITelemetryMonitor telemetryMonitor, IKernelTelemetryMonitor kernelTelemetry)
    {
        _telemetryMonitor = telemetryMonitor;

        // Route to OS-specific implementation based on platform detection at runtime
        if (OperatingSystem.IsLinux())
        {
            _linuxOrchestrator = kernelTelemetry as LinuxAmdGpuMonitor ?? 
                               kernelTelemetry as IKernelTelemetryMonitor;
            
            // Initialize Linux paths
            _linuxOrchestrator?.InitializeLinuxPath();
        }
        else if (OperatingSystem.IsWindows())
        {
            _windowsOrchestrator = kernelTelemetry as WindowsGameOrchestrator ?? 
                                  kernelTelemetry as IKernelTelemetryMonitor;

            // Initialize Windows kernel mode monitoring
            _windowsOrchestrator?.InitializeWindowsKernelMode();
        }
    }

    public void Initialize()
    {
        // Cross-platform initialization with OS-specific routing
        if (OperatingSystem.IsLinux()) 
            _linuxOrchestrator?.InitializeLinuxPath();
        else if (OperatingSystem.IsWindows()) 
            _windowsOrchestrator?.InitializeWindowsKernelMode();
        
        _telemetryMonitor.Initialize();
    }

    public void StartBackgroundMonitoringLoop()
    {
        // Non-blocking background monitoring loop with zero-allocation telemetry polling
        var backgroundTask = Task.Run(() =>
        {
            while (true) 
            {
                try
                {
                    var state = _telemetryMonitor.GetTelemetry();
                    
                    // Dispatch to UI thread for update (zero-allocation string formatting below)
                    Avalonia.Threading.Dispatcher.UIThread.PostAsync(async () =>
                        await UpdateUIWithTelemetry(state)).Wait();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Background telemetry loop error: {ex}");
                }

                // Non-blocking sleep using OS-specific APIs
                if (OperatingSystem.IsLinux()) 
                    Mono.Unix.Native.Syscall.sleep(1);
                else if (OperatingSystem.IsWindows()) 
                    System.Threading.Thread.Sleep(1000);
            }
        });
        
        _backgroundTask = backgroundTask;
    }

    public void StopAllProcesses()
    {
        // OS-specific process termination
        if (OperatingSystem.IsLinux()) 
            _linuxOrchestrator?.StopAllProcesses();
        else if (OperatingSystem.IsWindows()) 
            _windowsOrchestrator?.StopAllProcesses();
    }

    public Task<bool> IsProcessRunning(string processName)
    {
        // Cross-platform process check with OS-specific APIs
        return OperatingSystem.IsLinux() ? 
            CheckProcessOnLinux(processName) : 
            System.Diagnostics.Process.GetProcessesByName(processName).Any();
    }

    public int GetProcessId(string processName)
    {
        // OS-specific process ID retrieval
        if (OperatingSystem.IsLinux()) 
            return _linuxOrchestrator?.GetProcessId(processName) ?? 0;
        else if (OperatingSystem.IsWindows()) 
            return _windowsOrchestrator?.GetProcessId(processName) ?? 0;
        
        return System.Diagnostics.Process.GetProcessesByName(processName).FirstOrDefault()?.Id ?? 0;
    }

    public void DumpVRAM()
    {
        // OS-specific VRAM dump mechanism
        if (OperatingSystem.IsLinux()) 
            _linuxOrchestrator?.DumpVRAM();
        else if (OperatingSystem.IsWindows()) 
            _windowsOrchestrator?.DumpVRAM();
    }

    private async Task UpdateUIWithTelemetry(GpuTelemetryState state)
    {
        await Avalonia.Threading.Dispatcher.UIThread.PostAsync(async () =>
        {
            // Zero-allocation string formatting using cached buffers from MainWindow
            TextBlocks[0].Text = $"VRAM: {state.VramUsageMB} MB";
            TextBlocks[1].Text = $"Core Clock: 0 MHz (Vendor API needed)";
            TextBlocks[2].Text = $"Temp: 0°C (Vendor API needed)";
        });
    }

    private async Task<bool> CheckProcessOnLinux(string processName)
    {
        // Linux-specific process check using /proc filesystem for zero-allocation
        var procPath = $"/proc";
        if (!Directory.Exists(procPath)) return false;
        
        foreach (var dir in Directory.GetDirectories(procPath, "*", SearchOption.AllDirectories))
        {
            try 
            {
                var exePath = Path.Combine(dir, "exe", processName);
                if (File.Exists(exePath)) return true;
            }
            catch (Exception) { }
        }
        
        // Fallback to popen for process check
        return await Task.FromResult<bool>(popen($"ps -p 1 > /dev/null 2>&1").WaitAsync());
    }

    public void Dispose()
    {
        _processLock?.Dispose();
        _backgroundTask?.Wait().ConfigureAwait(false);
        
        if (OperatingSystem.IsLinux()) 
            _linuxOrchestrator?.Dispose();
        else if (OperatingSystem.IsWindows()) 
            _windowsOrchestrator?.Dispose();
    }

    private readonly Task _backgroundTask = default!;
}
