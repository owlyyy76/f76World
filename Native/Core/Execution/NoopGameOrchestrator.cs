using System.Threading.Tasks;

namespace f76World.Native.Core.Execution
{
    internal class NoopGameOrchestrator : IGameOrchestrator
    {
        public void Dispose() { }
        public void DumpVRAM() { }
        public int GetProcessId(string processName) => 0;
        public Task<bool> IsProcessRunning(string processName) => Task.FromResult(false);
        public void Initialize() { }
        public void StartBackgroundMonitoringLoop() { }
        public void StopAllProcesses() { }
    }
}
