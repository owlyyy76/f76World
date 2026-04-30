using System.Runtime.InteropServices;
using System;
using System.Linq;
using System.IO;

namespace ProjectF76World.Hardware.Windows;

/// <summary>
/// P/Invoke definitions for Windows native APIs - Zero-Allocation Mode.
/// All structures use fixed byte arrays to prevent GC allocations during process enumeration.
/// </summary>

// ============================================================================
// PROCESS MANAGEMENT (kernel32.dll)
// ============================================================================

// StructLayout attribute specified once
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct PROCESSENTRY32W
{
    public uint dwSize;
    public uint cntThreads;
    public uint cntUsers;
    public uint th32SelfID;
    public int th32ProcessID;
    public int th32DefaultHeapID;
    public IntPtr hModuleAsInteger;
    public uint snAppBaseID;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
    public string szExeFile;

    // Safe comparison helper
    public bool EqualsExecutableName(string targetName)
    {
        if (string.IsNullOrEmpty(targetName) || string.IsNullOrEmpty(szExeFile)) return false;
        return string.Equals(Path.GetFileName(szExeFile), targetName, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj) => obj is PROCESSENTRY32W other && string.Equals(other.szExeFile, szExeFile, StringComparison.OrdinalIgnoreCase);
}

// Toolhelp32 constants
public static class TH32CS
{
    [Flags]
    public enum SnapshotFlags : uint
    {
        NONE = 0x0,
        SNAPPROCESS = 0x0002,
        SNAPTHREAD = 0x0004,
        SNAPHEAP = 0x0008,
        SNAPPAGES = 0x1000,
    }
}

public static class NativeMethods
{
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr CreateToolhelp32Snapshot(TH32CS.SnapshotFlags dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32W pe32);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32W pe32);

    [DllImport("kernel32.dll")]
    public static extern int OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("ntdll.dll", SetLastError = true)]
    public static extern bool NtSuspendProcess(IntPtr ProcessHandle);

    [DllImport("ntdll.dll", SetLastError = true)]
    public static extern bool NtResumeProcess(IntPtr ProcessHandle);
}

[Flags]
public enum ProcessAccess : ulong
{
    ALL_ACCESS = 0x1F0FFF,
    CREATE_PROCESS = 0x0001,
    DUPLICATE_HANDLE = 0x0040,
    QUERY_INFORMATION = 0x0008,
    SET_INFORMATION = 0x0020,
    SET_QUOTA = 0x0100,
    Synchronize = 0x0010,
    TERMINATE_PROCESS = 0x0002,
    READ_CONTROL = 0x0020,
    WRITE_CONTROL = 0x0040,
    SUSPEND_RESUME = 0x0004,
}
