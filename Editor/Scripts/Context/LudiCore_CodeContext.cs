using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace IndieBuff.Editor
{
    public class IndieBuff_CodeContext
    {
        private static readonly string scanOutputPath = Path.Combine(Application.persistentDataPath, "IndieBuff/Context/IndieBuff_ProjectScan.json");

        private bool isScanning = false;
        private bool isBuildingGraph = false;
        private ProjectScanData scanData;

        private const int DefaultMapTokens = 1024;
        private Dictionary<string, object> codeMap = new Dictionary<string, object>();

        private static IndieBuff_CodeContext _instance;
        internal static IndieBuff_CodeContext Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new IndieBuff_CodeContext();
                }
                return _instance;
            }
        }


        public async void ScanProject()
        {
            if (isScanning)
            {
                return;
            }
            isScanning = true;

            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var projectPath = Application.dataPath;
                var files = Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories).ToList();

                var projectScanner = new IndieBuff_ProjectScanner();
                scanData = await projectScanner.ScanFiles(files, projectPath);

                // Save scan data to file
                var json = JsonConvert.SerializeObject(scanData);
                File.WriteAllText(scanOutputPath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error during project scan: {ex}");
            }
            finally
            {
                isScanning = false;
            }
        }

        public async Task<Dictionary<string, object>> BuildGraphAndGenerateMap()
        {

            if (isBuildingGraph)
            {
                return codeMap;
            }
            isBuildingGraph = true;
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();

                // Load scan data
                var json = File.ReadAllText(scanOutputPath);
                var scanData = JsonConvert.DeserializeObject<ProjectScanData>(json);


                var result = await Task.Run(() =>
                {
                    var graphBuilder = new IndieBuff_CodeGraphBuilder(DefaultMapTokens);
                    return graphBuilder.BuildGraphAndGenerateMap(scanData);
                });

                result = result.Replace("\r", "");

                // Save the map
                codeMap["map"] = result;

                //Debug.Log($"Graph building completed in {sw.ElapsedMilliseconds}ms");
                return codeMap;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error during graph building: {ex}");
                return codeMap;
            }
            finally
            {
                isBuildingGraph = false;
            }
        }

    }
}