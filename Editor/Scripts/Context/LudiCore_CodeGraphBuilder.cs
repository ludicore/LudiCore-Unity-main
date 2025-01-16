using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IndieBuff.Editor
{
    public class IndieBuff_CodeGraphBuilder
    {
        private readonly int maxMapTokens;
        private readonly ConcurrentDictionary<string, int> referenceCount;
        private readonly ConcurrentDictionary<string, List<SymbolDefinition>> fileSymbols;
        private readonly StringBuilder stringBuilder;
        
        public IndieBuff_CodeGraphBuilder(int maxMapTokens = 1024)
        {
            this.maxMapTokens = maxMapTokens;
            this.referenceCount = new ConcurrentDictionary<string, int>();
            this.fileSymbols = new ConcurrentDictionary<string, List<SymbolDefinition>>();
            this.stringBuilder = new StringBuilder();
        }

        public string BuildGraphAndGenerateMap(ProjectScanData scanData)
        {
            // Transfer data to concurrent
            foreach (var kvp in scanData.FileSymbols)
            {
                fileSymbols[kvp.Key] = kvp.Value;
            }
            foreach (var kvp in scanData.ReferenceCount)
            {
                referenceCount[kvp.Key] = kvp.Value;
            }

            var rankGraph = new IndieBuff_CodeFileRankGraph();
            var seenDefinitions = new Dictionary<string, (string File, double Weight)>();

            foreach (var (relativePath, symbolList) in fileSymbols)
            {
                var namespaceMul = GetNamespaceMultiplier(relativePath);

                foreach (var symbol in symbolList)
                {
                    var definitionKey = GetDefinitionKey(symbol);
                    var currentWeight = CalculateBaseWeight(symbol, namespaceMul);

                    if (seenDefinitions.TryGetValue(definitionKey, out var existing))
                    {
                        if (currentWeight <= existing.Weight)
                        {
                            rankGraph.AddReference(symbol.Name, relativePath);
                            continue;
                        }
                    }

                    seenDefinitions[definitionKey] = (relativePath, currentWeight);
                    rankGraph.AddDefinition(symbol.Name, relativePath);
                }
            }

            var fileRanks = rankGraph.CalculateRanks();

            foreach (var symbols in fileSymbols.Values)
            {
                foreach (var symbol in symbols)
                {
                    var fileRank = fileRanks.GetValueOrDefault(symbol.RelativePath, 0.0);
                    var refCount = referenceCount.GetValueOrDefault(symbol.Name, 0);

                    symbol.Rank = fileRank * Math.Sqrt(refCount);
                }
            }

            return GenerateMap(fileSymbols.Values.SelectMany(s => s).OrderByDescending(s => s.Rank).ToList());
        }


        // all my helper methods
        private static readonly Dictionary<string, double> UnityMethodWeights = new Dictionary<string, double>
    {
        {"Start", 0.1},
        {"Update", 0.1},
        {"FixedUpdate", 0.1},
        {"LateUpdate", 0.1},
        {"OnGUI", 0.1},
        {"OnDisable", 0.1},
        {"OnEnable", 0.1},
        {"Awake", 0.1},
        {"OnDestroy", 0.1}
    };

        private static readonly Dictionary<string, double> CommonMethodWeights = new Dictionary<string, double>
    {
        {"ToString", 0.1},
        {"Equals", 0.1},
        {"GetHashCode", 0.1},
        {"GetEnumerator", 0.1},
        {"CopyTo", 0.1},
        {"Contains", 0.1},
        {"Clear", 0.1},
        {"Add", 0.1},
        {"Remove", 0.1},
        {"SerializeObject", 0.1}
    };


        private double GetMethodMultiplier(SymbolDefinition symbol)
        {
            if (symbol.Kind != "method")
                return 1.0;

            if (UnityMethodWeights.TryGetValue(symbol.Name, out double weight))
                return weight;

            if (CommonMethodWeights.TryGetValue(symbol.Name, out weight))
                return weight;

            if (symbol.Name.StartsWith("get_") || symbol.Name.StartsWith("set_"))
                return 0.4;

            if (symbol.Parameters.Count == 0)
            {
                if (symbol.Name.StartsWith("Get") || symbol.Name.StartsWith("Set") || symbol.Name.StartsWith("Is"))
                    return 0.1;
            }

            return 1.0;
        }

        private double GetSymbolMultiplier(SymbolDefinition symbol)
        {
            var baseMul = symbol.Name.StartsWith("_") ? 0.1 : 1.0;

            if (symbol.Kind == "class")
            {
                if (symbol.Name.EndsWith("Service") || symbol.Name.EndsWith("Manager") || symbol.Name.EndsWith("Controller"))
                    baseMul *= 1.5;
            }

            if (symbol.Kind == "method")
            {
                if (symbol.Name.StartsWith("On") || symbol.Name.StartsWith("Handle"))
                    baseMul *= 0.7;
            }

            return baseMul;
        }

        private double GetNamespaceMultiplier(string relativePath)
        {
            var path = relativePath.ToLower();

            if (path.Contains("indiebuff") || path.Contains("/indiebuff") || path.Contains("indiebuff/")) return 0.0;
            /*if (path.Contains("internal")) return 2.0;
            if (path.Contains("shared")) return 1.5;
            if (path.Contains("models")) return 0.7;
            if (path.Contains("editor")) return 0.5;*/
            return 1.0;
        }
        private double CalculateBaseWeight(SymbolDefinition symbol, double namespaceMul) =>
            namespaceMul * GetSymbolMultiplier(symbol) * GetMethodMultiplier(symbol);

        private string GetDefinitionKey(SymbolDefinition symbol)
        {
            var key = $"{symbol.Kind}:{symbol.Name}";
            if (symbol.Kind == "method")
            {
                var paramTypes = string.Join(",", symbol.Parameters.Select(p => p.Split(' ')[0]));
                key += $"({paramTypes})";
            }
            return key;
        }

        private string GenerateMap(List<SymbolDefinition> allSymbols)
        {
            int numSymbols = allSymbols.Count;
            int lowerBound = 0;
            int upperBound = numSymbols;
            string bestTree = null;
            int bestTreeTokens = 0;

            // Binary search for optimal number of symbols to include
            while (lowerBound <= upperBound)
            {
                int middle = (lowerBound + upperBound) / 2;
                var selectedSymbols = allSymbols.Take(middle).ToList();
                var content = BuildMapContent(selectedSymbols);

                int numTokens = EstimateTokenCount(content);

                // Match original 15% error tolerance
                double pctError = Math.Abs(numTokens - maxMapTokens) / (double)maxMapTokens;
                const double okError = 0.15;

                if ((numTokens <= maxMapTokens && numTokens > bestTreeTokens) || pctError < okError)
                {
                    bestTree = content;
                    bestTreeTokens = numTokens;

                    if (pctError < okError)
                        break;
                }

                if (numTokens < maxMapTokens)
                    lowerBound = middle + 1;
                else
                    upperBound = middle - 1;
            }

            return bestTree ?? string.Empty;
        }

        private string BuildMapContent(List<SymbolDefinition> symbols)
        {
            stringBuilder.Clear();
            var groupedByFile = symbols
                .GroupBy(s => s.RelativePath)
                .OrderBy(g => g.Key);

            foreach (var fileGroup in groupedByFile)
            {
                if (!fileGroup.Any()) continue;

                stringBuilder.AppendLine($"{fileGroup.Key}:");
                stringBuilder.AppendLine("│");

                var orderedSymbols = fileGroup
                    .OrderByDescending(s => s.Rank)
                    .ThenBy(s => s.Line);

                var lastLine = -1;
                foreach (var symbol in orderedSymbols)
                {
                    if (lastLine != -1 && symbol.Line - lastLine > 1)
                    {
                        stringBuilder.AppendLine("⋮...");
                    }

                    switch (symbol.Kind)
                    {
                        case "class":
                            stringBuilder.AppendLine($"│{symbol.Visibility} class {symbol.Name}");
                            break;

                        case "method":
                            var parameters = string.Join(", ", symbol.Parameters);
                            stringBuilder.AppendLine($"│ {symbol.Visibility} {symbol.ReturnType} {symbol.Name}({parameters})");
                            break;

                        case "property":
                            stringBuilder.AppendLine($"│ {symbol.Visibility} {symbol.ReturnType} {symbol.Name}");
                            break;
                    }

                    lastLine = symbol.Line;
                }

                stringBuilder.AppendLine("⋮...");
            }

            return stringBuilder.ToString();
        }

        private int EstimateTokenCount(string text) => text.Length / 4;
    }

}