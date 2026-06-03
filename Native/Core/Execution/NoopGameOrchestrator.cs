using System;
using System.Threading.Tasks;

namespace f76World.Native.Core.Execution
{
    internal class NoopGameOrchestrator : IGameOrchestrator
    {
        public void Dispose() 
        { 
            GC.SuppressFinalize(this);
        }
        
        public void DumpVRAM() { }
        public int GetProcessId(string _) => 0;
        public Task<bool> IsProcessRunning(string _) => Task.FromResult(false);
        public void Initialize() { }
        public void StartBackgroundMonitoringLoop() { }
        public void StopAllProcesses() { }
        
        public Task PurgeVramAsync() => Task.CompletedTask;
        public Task LaunchGameAsync(string _) => Task.CompletedTask;
        public void Stop() { }
    }
}
