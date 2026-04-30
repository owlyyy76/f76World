namespace ProjectF76World.Hardware;

/// <summary>
/// Base interface for GPU telemetry monitoring across all platforms.
/// </summary>
public interface ITelemetryMonitor : IDisposable
{
    /// <summary>
    /// Get current GPU telemetry state as a zero-allocation struct.
    /// Returns GpuTelemetryState with VRAM, Core Clock, and Temperature values.
    /// </summary>
    GpuTelemetryState GetTelemetry();
}
