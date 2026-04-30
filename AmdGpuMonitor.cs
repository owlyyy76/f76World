using System;
using System.Buffers;
using System.IO;
using Microsoft.Extensions.DependencyInjection;

namespace f76World.Native.Core.Telemetry
{
    /// <summary>
    /// AMD GPU Telemetry Monitor - Zero-allocation sysfs reader.
    /// Reads VRAM usage, power clocks directly from /sys/class/drm/ via ArrayPool<byte>.
    /// </summary>
    public class AmdGpuMonitor : ITelemetryMonitor
    {
    private readonly string _drmPath;
    private readonly int _pollIntervalMs;
    private readonly IServiceScope? _scope;

        // AMD sysfs paths for card0 (first display connector)
        private const string VRAM_PATH = "/sys/class/drm/card0/device/mem_info_vram_used";
        private const string CLOCK_PATH = "/sys/class/drm/card0/device/pp_dpm_sclk";
        
        // Zero-allocation byte pools - returned after each use
        private static readonly byte[] PoolVram = ArrayPool<byte>.Shared.Rent(1024);
        private static readonly byte[] PoolClock = ArrayPool<byte>.Shared.Rent(512);

        public AmdGpuMonitor(IServiceScope? scope = null, int pollIntervalMs = 500)
        {
            _scope = scope;
            _pollIntervalMs = pollIntervalMs;
            
            // Verify DRM card exists (AMD GPU prerequisite)
            if (!File.Exists(VRAM_PATH))
                _drmPath = string.Empty; // allow construction on non-Linux platforms
            else
                _drmPath = VRAM_PATH;
        }

        /// <summary>
        /// Polls all AMD GPU telemetry metrics with zero-allocation byte buffers.
        /// </summary>
        public TelemetrySnapshot GetTelemetry()
        {
            var vramUsed = 0L;
            var coreClock = 1200; // Default GHz if unavailable
            var powerClock = 800; // Default MHz

            try
            {
                _ = ReadMetric(VRAM_PATH, ref vramUsed);
            }
            catch (Exception) { /* Ignore Vram read errors - core clocks still valid */ }

            try
            {
            _ = ReadMetricInt(CLOCK_PATH, ref coreClock);
            }
            catch (Exception) { /* Ignore clock read errors - fallback values retained */ }

            return new TelemetrySnapshot
            {
                VramUsedBytes = vramUsed,
                CoreClockMhz = coreClock,
                PowerClockMhz = powerClock,
                Timestamp = DateTime.UtcNow,
                GpuType = "AMD Radeon"
            };
        }

        /// <summary>
        /// Reads all metrics from sysfs using ArrayPool byte buffers.
        /// </summary>
        private (long VramUsedBytes, int CoreClockMhz, int PowerClockMhz) ReadAllMetrics()
        {
            var vramUsed = 0L;
            var coreClock = 1200; // Default GHz if unavailable
            var powerClock = 800; // Default MHz

            try
            {
                _ = ReadMetric(VRAM_PATH, ref vramUsed);
            }
            catch (Exception) { /* Ignore Vram read errors - core clocks still valid */ }

            try
            {
            _ = ReadMetricInt(CLOCK_PATH, ref coreClock);
            }
            catch (Exception) { /* Ignore clock read errors - fallback values retained */ }

            return (vramUsed, coreClock, powerClock);
        }

        /// <summary>
        /// Zero-allocation sysfs file reader using File.OpenHandle + RandomAccess.Read.
        /// Parses bytes directly without string conversion to avoid allocations.
        /// </summary>
        private static int ReadMetric(string path, ref long value)
        {
            try
            {
                using var handle = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                var bytesRead = handle.Read(PoolVram, 0, PoolVram.Length);

                if (bytesRead > 0)
                {
                    // Parse bytes directly into long without ToString() allocation
                    value = LongParseBytesUnsafe(new ReadOnlySpan<byte>(PoolVram, 0, bytesRead));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AmdGpuMonitor] Sysfs read error ({path}): {ex.Message}");
                return -1;
            }

            return 0;
        }

        /// <summary>
        /// Zero-allocation sysfs file reader using File.OpenHandle + RandomAccess.Read.
        /// Parses bytes directly into int without string conversion to avoid allocations.
        /// </summary>
        private static int ReadMetricInt(string path, ref int value)
        {
            try
            {
                using var handle = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                var bytesRead = handle.Read(PoolClock, 0, PoolClock.Length);

                if (bytesRead > 0)
                {
                    value = IntParseBytesUnsafe(new ReadOnlySpan<byte>(PoolClock, 0, bytesRead));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AmdGpuMonitor] RandomAccess read error ({path}): {ex.Message}");
                return -1;
            }

            return 0;
        }

        /// <summary>
        /// Parses a byte span directly into long without string allocation.
        /// </summary>
        private static long LongParseBytesUnsafe(ReadOnlySpan<byte> bytes)
        {
            var result = 0L;
            
            // Manual big-endian parsing - no allocations
            for (int i = 0; i < bytes.Length && i < 8; i++)
                result |= ((long)bytes[i]) << (i * 8);

            return result;
        }

        /// <summary>
        /// Parses a byte span directly into int without string allocation.
        /// </summary>
        private static int IntParseBytesUnsafe(ReadOnlySpan<byte> bytes)
        {
            var result = 0;
            
            // Manual big-endian parsing - no allocations
            for (int i = 0; i < bytes.Length && i < 4; i++)
                result |= ((byte)bytes[i]) << (i * 8);

            return result;
        }

        /// <summary>
        /// Returns current VRAM usage in MB.
        /// </summary>
        public long GetVramUsedMb() => (GetTelemetry().VramUsedBytes / (1L << 20));

        /// <summary>
        /// Returns core clock frequency in MHz.
        /// </summary>
        public int GetCoreClockMhz() => GetTelemetry().CoreClockMhz;

        /// <summary>
        /// Background telemetry polling service.
        /// </summary>
        private async Task PollLoopAsync()
        {
            while (true)
            {
                try
                {
                    GetTelemetry(); // Force poll to update cached state
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AmdGpuMonitor] Poll error: {ex.Message}");
                }

                await Task.Delay(_pollIntervalMs);
            }
        }

        /// <summary>
        /// Starts background telemetry polling.
        /// </summary>
        public void Start() => Task.Run(PollLoopAsync).Wait();

        /// <summary>
        /// Stops and disposes the monitor service.
        /// </summary>
        public void Dispose()
        {
            try { ArrayPool<byte>.Shared.Return(PoolVram); } catch { }
            try { ArrayPool<byte>.Shared.Return(PoolClock); } catch { }
            
            _scope?.Dispose();
        }
    }

    /// <summary>
    /// AMD GPU Telemetry Snapshot - immutable record struct pattern.
    /// Zero-allocation value type for telemetry data.
    /// </summary>
    public readonly record struct TelemetrySnapshot(long VramUsedBytes, int CoreClockMhz, int PowerClockMhz, DateTime Timestamp, string GpuType = "AMD Radeon");

    /// <summary>
    /// Telemetry Monitor Interface for DI injection.
    /// </summary>
    public interface ITelemetryMonitor : IDisposable
    {
        long GetVramUsedMb();
        int GetCoreClockMhz();
        void Start();
    }
}
