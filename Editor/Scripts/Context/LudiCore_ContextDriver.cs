using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;


namespace IndieBuff.Editor
{
    internal class IndieBuff_ContextDriver
    {
        private static IndieBuff_ContextDriver _instance;
        internal string ContextObjectString = "";

        internal static IndieBuff_ContextDriver Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new IndieBuff_ContextDriver();
                }
                return _instance;
            }
        }

        public async Task<string> BuildAllContext(string prompt)
        {

            // build user selected context
            Dictionary<string, object> selectionMap = await IndieBuff_UserSelectedContext.Instance.BuildUserContext();

            // Build code context
            Dictionary<string, object> codeMap = await IndieBuff_CodeContext.Instance.BuildGraphAndGenerateMap();

            // Build scene context
            Dictionary<string, object> sceneMap = await IndieBuff_SceneContext.Instance.BuildRankedSceneContext(prompt);

            // Build asset context
            Dictionary<string, object> assetMap = await IndieBuff_AssetContextRanker.Instance.BuildRankedAssetContext(prompt);

            // Build project context
            Dictionary<string, object> projectMap = IndieBuff_ProjectContext.Instance.BuildProjectMap();


            var settings = new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                Formatting = Formatting.Indented,
                MaxDepth = null,
                NullValueHandling = NullValueHandling.Ignore
            };

            var contextData = new
            {
                selectionMap,
                codeMap,
                sceneMap,
                assetMap,
                projectMap
            };

            ContextObjectString = JsonConvert.SerializeObject(new { context = contextData }, settings);

            return ContextObjectString;

        }
    }
}