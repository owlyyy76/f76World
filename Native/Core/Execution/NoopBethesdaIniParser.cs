using System.Collections.Generic;

namespace f76World.Native.Core.Execution
{
    internal class NoopBethesdaIniParser : IBethesdaIniParser
    {
        public int KeyCount => 0;
        public int SectionCount => 0;
        public void Dispose() { }
        
        public List<string> GetSectionKeys(string section)
        {
            return new List<string>();
        }
        
        public void Parse(string iniPath) { }
        public bool TryGetValue(string section, string key, out string value) { value = string.Empty; return false; }
        public void Save(string iniPath) { }
    }
}
