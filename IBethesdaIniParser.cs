using System;
using System.Collections.Generic;

namespace f76World.Native.Core.Execution
{
    /// <summary>
    /// Bethesda INI Parser Interface - Zero-allocation INI parsing for Fallout 76 config files.
    /// Parses Fallout76Prefs.ini without string allocations using ReadOnlySpan<char>.
    /// </summary>
    public interface IBethesdaIniParser : IDisposable
    {
        void Parse(string iniPath);
        bool TryGetValue(string section, string key, out string value);
        List<string> GetSectionKeys(string section);
        void Save(string iniPath);
        int SectionCount { get; }
        int KeyCount { get; }
    }
}
