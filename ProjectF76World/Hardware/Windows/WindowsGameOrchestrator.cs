using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ProjectF76World.Hardware.Windows;

public class WindowsGameOrchestrator : IGameOrchestrator, IDisposable
{
    private static readonly ArrayPool<int> _pidPool = ArrayPool<int>.Shared;
    private int[]? _suspendedPids;
    private int _pidCount;

    // Definicje P/Invoke zostały wciągnięte i uproszczone dla zachowania monolitu
    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern int NtSuspendProcess(IntPtr processHandle);

    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern int NtResumeProcess(IntPtr processHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    private const uint PROCESS_SUSPEND_RESUME = 0x0800;

    public void Initialize()
    {
        _suspendedPids = _pidPool.Rent(128);
        _pidCount = 0;
    }

    public Task<bool> IsProcessRunning(string processName)
    {
        var processes = Process.GetProcessesByName(processName);
        bool isRunning = processes.Length > 0;
        foreach (var p in processes) p.Dispose();
        return Task.FromResult(isRunning);
    }

    public int GetProcessId(string processName)
    {
        var processes = Process.GetProcessesByName(processName);
        if (processes.Length > 0)
        {
            int id = processes[0].Id;
            foreach (var p in processes) p.Dispose();
            return id;
        }
        return 0;
    }

    public void DumpVRAM()
    {
        int pid = GetProcessId("steamwebhelper");
        if (pid <= 0) return;

        IntPtr processHandle = OpenProcess(PROCESS_SUSPEND_RESUME, false, pid);
        if (processHandle != IntPtr.Zero)
        {
            NtSuspendProcess(processHandle);
            Thread.Sleep(200); // Wymuszenie czyszczenia kolejki DXGI
            NtResumeProcess(processHandle);
            CloseHandle(processHandle);
        }
    }

    public void Dispose()
    {
        if (_suspendedPids != null)
        {
            _pidPool.Return(_suspendedPids);
            _suspendedPids = null;
        }
    }
}