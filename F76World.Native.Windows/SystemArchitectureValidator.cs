// SystemArchitectureValidator.cs - Tier 1: OS State Detection
// Zero-allocation static analyzer for Windows 11 + ARM/X64 bifurcation awareness

using System;
using System.Runtime.InteropServices;

namespace F76World.Native.Windows
{
    /// <summary>
    /// Tier 1 Architecture Validator - Detects host OS state without managed allocations.
    /// Provides runtime safety gates for optimization engine execution.
    /// </summary>
    public static class SystemArchitectureValidator
    {
        private const int BUILD_24H2_THRESHOLD = 26100;
        
        /// <summary>
        /// Detects if we are running Windows 11 Build 24H2 or newer (Windows 11 23H2+).
        /// Uses Environment.OSVersion.Version.Build - zero-allocation.
        /// </summary>
        public static bool IsModernWindows11
        {
            get
            {
                var build = Environment.OSVersion.Version.Build;
                return build >= BUILD_24H2_THRESHOLD;
            }
        }

        /// <summary>
        /// Detects if we are running on ARM64 architecture (Snapdragon 26H1).
        /// Uses RuntimeInformation.ProcessArchitecture - zero-allocation.
        /// </summary>
        public static bool IsArmArchitecture
        {
            get
            {
                var arch = RuntimeInformation.ProcessArchitecture;
                return arch == Architecture.Arm64 || 
                       (arch == Architecture.Arm && !IsX86Compatible(arch));
            }
        }

        /// <summary>
        /// Detects if we are running on X64 architecture.
        /// </summary>
        public static bool IsX64Architecture => RuntimeInformation.ProcessArchitecture == Architecture.X64;

        /// <summary>
        /// Checks if ARM is x86-compatible (ARM64EC).
        /// </summary>
        private static bool IsX86Compatible(Architecture arch)
        {
            var archString = RuntimeInformation.OSDescription?.ToLowerInvariant();
            return arch == Architecture.Arm && 
                   !archString.Contains("arm64") && 
                   !archString.Contains("x64");
        }

        /// <summary>
        /// Returns the build number as an integer.
        /// </summary>
        public static int GetBuildNumber() => Environment.OSVersion.Version.Build;

        /// <summary>
        /// Returns the architecture name string (safe, non-hot path).
        /// </summary>
        public static string GetArchitectureName() => RuntimeInformation.ProcessArchitecture.ToString();

        /// <summary>
        /// Checks if we're on a Windows 11 build that supports DWM optimizations.
        /// </summary>
        public static bool SupportsDwmOptimizations => IsModernWindows11 && !IsArmArchitecture;
    }

    // MARKER: For integration with DI container
    public interface ISystemValidator
    {
        bool IsModernWindows11 { get; }
        bool IsArmArchitecture { get; }
        bool IsX64Architecture { get; }
    }
}
