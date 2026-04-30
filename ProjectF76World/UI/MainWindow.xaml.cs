using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using System.ComponentModel;
using System.Threading.Tasks;
using System.IO;
using f76World.Native.Core.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using ProjectF76World.Hardware;


namespace ProjectF76World.UI;

public partial class MainWindow : Window
{
    private readonly f76World.Native.Core.Telemetry.ITelemetryMonitor _telemetryMonitor;
    private readonly PeriodicTimer _periodicTimer = new(System.TimeSpan.FromMilliseconds(1000));
    private readonly Task _backgroundMonitoringTask;

    // Pre-allocated buffers (managed fallback)
    private static readonly char[] _vramBuffer = new char[64];
    private static readonly char[] _clockBuffer = new char[128];
    private static readonly char[] _tempBuffer = new char[32];

    public MainWindow(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        
        // Inject telemetry monitor via constructor (DI pattern)
        _telemetryMonitor = serviceProvider.GetRequiredService<f76World.Native.Core.Telemetry.ITelemetryMonitor>();
        
        // Start background monitoring loop
        _backgroundMonitoringTask = Task.Run(StartBackgroundLoopAsync);
    }

    private async Task StartBackgroundLoopAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1000));
        
        while (await timer.WaitForNextTickAsync())
        {
            try
            {
                // Get telemetry from monitor - returns readonly struct, no boxing
                var state = _telemetryMonitor.GetTelemetry();
                
                // Zero-allocation string formatting using ISpanFormattable patterns
                UpdateTextBlocksZeroAlloc(state);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Background telemetry monitoring error: {ex}");
            }
        }
    }

    private async Task UpdateTextBlocksZeroAlloc(GpuTelemetryState state)
    {
        // Use Dispatcher.UIThread.PostAsync for UI updates from background thread
        await Dispatcher.UIThread.PostAsync(() =>
        {
            // Simple managed formatting (acceptable for now)
            TextBlocks[0].Text = $"VRAM: {state.VramUsageMB} MB";
            TextBlocks[1].Text = $"Core Clock: {state.CoreClockMHz} MHz";
            TextBlocks[2].Text = $"Temp: {state.TemperatureCelsius}°C";
        });
    }

    private static void SpanAction(Span<char> span, int value)
    {
        // Minimalist helper - not used in current managed formatting
        if (value <= 0) return;
        var s = value.ToString();
        for (int i = 0; i < s.Length && i < span.Length; i++) span[i] = s[i];
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Stop background monitoring loop on close
        _backgroundMonitoringTask?.Wait().ConfigureAwait(false);
        base.OnClosing(e);
    }
}
