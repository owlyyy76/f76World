namespace ProjectF76World.Hardware;

/// <summary>
/// Zero-allocation telemetry state struct - no boxing, no class overhead.
/// All fields are value types for stack allocation efficiency.
/// </summary>
public readonly struct GpuTelemetryState
{
    // VRAM Usage in megabytes (uint to prevent negative values)
    public uint VramUsageMB { get; init; }

    // Core Clock frequency in MHz
    public int CoreClockMHz { get; init; }

    // GPU Temperature in Celsius
    public int TemperatureCelsius { get; init; }

    /// <summary>
    /// Create a default telemetry state (0 values) for initialization.
    /// </summary>
    public static GpuTelemetryState Default => new()
    {
        VramUsageMB = 0,
        CoreClockMHz = 0,
        TemperatureCelsius = 0
    };

    /// <summary>
    /// Create telemetry state from the provided values.
    /// </summary>
    public GpuTelemetryState(uint vram, int clock, int temp) : this()
    {
        VramUsageMB = vram;
        CoreClockMHz = clock;
        TemperatureCelsius = temp;
    }

    /// <summary>
    /// Zero-allocation equality check using struct value comparison.
    /// </summary>
    public bool Equals(GpuTelemetryState other) => 
        VramUsageMB == other.VramUsageMB && 
        CoreClockMHz == other.CoreClockMHz && 
        TemperatureCelsius == other.TemperatureCelsius;

    /// <summary>
    /// Hash code for collection operations without allocation.
    /// </summary>
    public override int GetHashCode() => 
        unchecked(VramUsageMB.GetHashCode() ^ CoreClockMHz.GetHashCode() ^ TemperatureCelsius.GetHashCode());
}
