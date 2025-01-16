using System;
using System.Collections.Generic;

namespace IndieBuff.Editor
{
    [Serializable]
    public class ProjectScanData
    {
        public Dictionary<string, List<SymbolDefinition>> FileSymbols { get; set; }
        public Dictionary<string, int> ReferenceCount { get; set; }
    }

    [Serializable]
    public class SymbolDefinition
    {
        public string Name { get; set; }
        public string Kind { get; set; }
        public int Line { get; set; }
        public List<string> Parameters { get; set; }
        public string ReturnType { get; set; }
        public string Visibility { get; set; }
        public string FilePath { get; set; }
        public string RelativePath { get; set; }
        public double Rank { get; set; }

        public SymbolDefinition()
        {
            Parameters = new List<string>();
        }
    }
}