using System.Collections.Generic;
using System.Linq;
using QuikGraph;
using QuikGraph.Algorithms.Ranking;

namespace IndieBuff.Editor
{
    public class IndieBuff_CodeFileRankGraph
    {
        private BidirectionalGraph<string, IEdge<string>> graph;
        private Dictionary<string, HashSet<string>> defines;
        private Dictionary<string, Dictionary<string, int>> references;

        public IndieBuff_CodeFileRankGraph()
        {
            graph = new BidirectionalGraph<string, IEdge<string>>(true);
            defines = new Dictionary<string, HashSet<string>>();
            references = new Dictionary<string, Dictionary<string, int>>();
        }

        public void AddDefinition(string identifier, string definingFile)
        {
            if (!defines.ContainsKey(identifier))
                defines[identifier] = new HashSet<string>();
            defines[identifier].Add(definingFile);
            if (!graph.ContainsVertex(definingFile))
                graph.AddVertex(definingFile);
        }

        public void AddReference(string identifier, string referencingFile)
        {
            if (!references.ContainsKey(identifier))
                references[identifier] = new Dictionary<string, int>();

            var refDict = references[identifier];
            if (!refDict.ContainsKey(referencingFile))
                refDict[referencingFile] = 0;
            refDict[referencingFile]++;

            if (!graph.ContainsVertex(referencingFile))
                graph.AddVertex(referencingFile);
        }

        public Dictionary<string, double> CalculateRanks()
        {
            var idents = defines.Keys.Intersect(references.Keys);

            foreach (var ident in idents)
            {
                var definers = defines[ident];
                var refs = references[ident];

                foreach (var (referencer, numRefs) in refs)
                {
                    foreach (var definer in definers)
                    {
                        graph.AddEdge(new Edge<string>(referencer, definer));
                    }
                }
            }

            var algorithm = new PageRankAlgorithm<string, IEdge<string>>(graph);
            algorithm.Compute();

            return (Dictionary<string, double>)algorithm.Ranks;
        }
    }
}