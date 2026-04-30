using System.Diagnostics;
using ProjectF76World.Hardware;

namespace ProjectF76World.Hardware.Windows;

/// <summary>
/// Windows GPU monitor using WDDM PerformanceCounter API.
/// Zero-allocation implementation - no string allocations in telemetry retrieval.
/// </summary>
public class WindowsGpuMonitor : ITelemetryMonitor, IDisposable
{
    // Cached performance counter for GPU memory monitoring
    private readonly PerformanceCounter? _gpuMemoryCounter;

    // WDDM Category name (standard Windows naming)
    private static readonly string GPU_MEMORY_CATEGORY = "GPU Adapter Memory";

    /// <summary>
    /// Constructor - initializes PerformanceCounter category and caches the dedicated usage counter.
    /// </summary>
    public WindowsGpuMonitor()
    {
        try
        {
            // Initialize PerformanceCounterCategory ONCE during construction (zero-allocation pattern)
            var category = new PerformanceCounterCategory(GPU_MEMORY_CATEGORY);

            if (category.GetInstanceNames().Length > 0)
            {
                // Cache the dedicated usage counter instance for zero-allocation retrieval
                _gpuMemoryCounter = GetDedicatedUsageCounter(category);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"GPU monitor initialization failed: {ex.Message}");
            // Continue with dummy values if initialization fails
        }

        // Initialize for Windows kernel mode monitoring
        InitializeWindowsKernelMode();
    }

    /// <summary>
    /// Get the cached dedicated usage performance counter.
    /// </summary>
    private static PerformanceCounter? GetDedicatedUsageCounter(PerformanceCounterCategory category)
    {
        try
        {
            foreach (var name in category.GetInstanceNames())
            {
                var counter = new PerformanceCounter(category, "Dedicated Usage", name);
                
                // Check if counter has valid data available
                if (counter.HasValidData()) return counter;
            }

            return null;
        }
        catch (Exception) { return null; }
    }

    /// <summary>
    /// Get current GPU telemetry state.
    /// VRAM: Uses cached PerformanceCounter.NextValue() - zero-allocation call.
    /// Core Clock & Temp: Return safe dummy values (vendor APIs needed for native exposure).
    /// </summary>
    public GpuTelemetryState GetTelemetry()
    {
        uint vramUsageMB = 0;

        // Zero-allocation retrieval using cached PerformanceCounter instance
        if (_gpuMemoryCounter != null)
        {
            try
            {
                var bytesUsed = _gpuMemoryCounter.NextValue();
                
                // Convert bytes to megabytes (zero-allocation arithmetic only)
                vramUsageMB = (uint)(bytesUsed / 1024 / 1024);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"GPU telemetry retrieval error: {ex.Message}");
                // Return default values on failure
            }
        }

        return new GpuTelemetryState(vramUsageMB, 0, 0);
    }

    /// <summary>
    /// Initialize Windows kernel mode monitoring.
    /// For stub implementation - will be fleshed out with WDDM driver hooks later.
    /// </summary>
    public void InitializeWindowsKernelMode()
    {
        // Placeholder for future kernel-mode telemetry injection
        // This would hook into WDDM's memory manager via DDK in production
    }

    /// <summary>
    /// Cleanup Windows resources and release PerformanceCounter instances.
    /// </summary>
    public void Dispose()
    {
        _gpuMemoryCounter?.Dispose();
        
        // Dispose the category if it was created during initialization (not thread-safe, so we check)
        try
        {
            var category = new PerformanceCounterCategory(GPU_MEMORY_CATEGORY);
            category.Dispose();
        }
        catch { /* Ignore disposal errors */ }
    }

    /// <summary>
    /// Check if GPU monitoring is available on this system.
    /// </summary>
    public bool IsMonitoringAvailable() => _gpuMemoryCounter != null;
}
