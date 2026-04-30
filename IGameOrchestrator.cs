using System;
using System.Threading.Tasks;

namespace f76World.Native.Core.Execution
{
    /// <summary>
    /// Game Orchestrator Interface - Native Linux process management via gamemoderun/gamescope.
    /// Manages SIGSTOP/SIGCONT on steamwebhelper for VRAM dumps during gameplay.
    /// </summary>
    public interface IGameOrchestrator : IDisposable
    {
        Task PurgeVramAsync();
        Task LaunchGameAsync(string gamePath);
        void Stop();
    }
}
