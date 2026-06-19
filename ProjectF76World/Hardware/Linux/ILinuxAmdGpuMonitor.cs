using ProjectF76World;

namespace ProjectF76World.Hardware.Linux;

/// <summary>
/// Linux-specific AMD GPU monitor interface - extends ITelemetryMonitor
/// with Linux-only initialization/cleanup methods.
/// </summary>
public interface ILinuxAmdGpuMonitor : IDisposable, IKernelTelemetryMonitor
{
    /// <summary>
    /// Initialize Linux GPU paths (/sys/class/drm/).
    /// </summary>
    void InitializeLinuxPath();

    /// <summary>
    /// Cleanup Linux GPU resources and free memory caches.
    /// </summary>
    void CleanupLinuxResources();
}
