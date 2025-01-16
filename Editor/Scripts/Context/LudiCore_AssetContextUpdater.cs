using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;


namespace IndieBuff.Editor
{
    public class IndieBuff_AssetContextUpdater : AssetPostprocessor
    {

        private static readonly string CacheFilePath = Path.Combine(Application.persistentDataPath, "IndieBuff/Context/IndieBuff_AssetCache.json");
        private static readonly string scanOutputPath = Path.Combine(Application.persistentDataPath, "IndieBuff/Context/IndieBuff_ProjectScan.json");
        public static List<IndieBuff_AssetNode> assetItems = new List<IndieBuff_AssetNode>();
        public static Action onAssetContextUpdated;

        private static ProjectScanData currentScanData;


        static IndieBuff_AssetContextUpdater()
        {
            LoadCache();
        }

        public static void Initialize()
        {
            ScanAssets();
            IndieBuff_CodeContext.Instance.ScanProject();
        }

        private static void ScanAssets()
        {
            assetItems = new List<IndieBuff_AssetNode>();
            var allAssetPaths = AssetDatabase.GetAllAssetPaths();

            foreach (var path in allAssetPaths)
            {
                if (Directory.Exists(path) || path.EndsWith(".meta") || path.StartsWith("Assets/IndieBuff/") || path.EndsWith("AssetCache.json"))
                    continue;

                if (path.StartsWith("Assets/"))
                {
                    var child = new IndieBuff_AssetNode
                    {
                        Name = Path.GetFileName(path),
                        Path = path,
                        Type = AssetDatabase.GetMainAssetTypeAtPath(path)?.Name ?? "Unknown",
                        LastModified = File.GetLastWriteTime(path),
                        Added = DateTime.Now,
                    };
                    assetItems.Add(child);
                }

            }

            SaveCache();
        }

        private static void LoadCache()
        {
            if (File.Exists(CacheFilePath))
            {
                var json = File.ReadAllText(CacheFilePath);
                assetItems = Newtonsoft.Json.JsonConvert.DeserializeObject<List<IndieBuff_AssetNode>>(json) ?? new List<IndieBuff_AssetNode>();
            }
            else
            {
                ScanAssets();
            }
        }

        private static void AddOrUpdateAsset(string path)
        {
            if (path.EndsWith("AssetCache.json") || Directory.Exists(path) || path.EndsWith(".meta"))
                return;

            var existingNode = assetItems.Find(node => node.Path == path);
            if (existingNode != null)
            {
                existingNode.LastModified = File.GetLastWriteTime(path);
            }
            else
            {
                var newNode = new IndieBuff_AssetNode
                {
                    Name = Path.GetFileName(path),
                    Path = path,
                    Type = AssetDatabase.GetMainAssetTypeAtPath(path)?.Name ?? "Unknown",
                    LastModified = File.GetLastWriteTime(path),
                    Added = DateTime.Now,
                };
                assetItems.Add(newNode);
            }

            SaveCache();
        }

        private static void RemoveAsset(string path)
        {
            var nodeToRemove = assetItems.Find(node => node.Path == path);
            if (nodeToRemove != null)
            {
                assetItems.Remove(nodeToRemove);
                SaveCache();
            }
        }

        private static void SaveCache()
        {
            string directoryPath = Path.GetDirectoryName(CacheFilePath);
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(assetItems, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(CacheFilePath, json);
            AssetDatabase.Refresh();
            onAssetContextUpdated?.Invoke();
        }

        private static async void ProcessChangedFiles(string[] changedFiles, string[] deletedFiles)
        {
            try
            {
                // Load existing scan data
                LoadCurrentScanData();

                if (currentScanData == null)
                {
                    return;
                }

                var projectPath = Application.dataPath;
                var projectScanner = new IndieBuff_ProjectScanner();

                // Process changed/new files
                var updatedData = await projectScanner.ScanFiles(changedFiles.ToList(), projectPath);

                // Update the current scan data with new information
                foreach (var kvp in updatedData.FileSymbols)
                {
                    currentScanData.FileSymbols[kvp.Key] = kvp.Value;
                }

                // Update reference counts
                foreach (var kvp in updatedData.ReferenceCount)
                {
                    if (currentScanData.ReferenceCount.ContainsKey(kvp.Key))
                    {
                        currentScanData.ReferenceCount[kvp.Key] += kvp.Value;
                    }
                    else
                    {
                        currentScanData.ReferenceCount[kvp.Key] = kvp.Value;
                    }
                }

                // Remove deleted files
                foreach (var deletedFile in deletedFiles)
                {
                    var relativePath = Path.GetRelativePath(projectPath, deletedFile);
                    currentScanData.FileSymbols.Remove(relativePath);
                    // don't remove reference counts as they might still be valid from other files
                }

                SaveCurrentScanData();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error processing changed files: {ex}");
            }
        }

        private static void LoadCurrentScanData()
        {
            try
            {
                if (File.Exists(scanOutputPath))
                {
                    var json = File.ReadAllText(scanOutputPath);
                    currentScanData = JsonConvert.DeserializeObject<ProjectScanData>(json);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading scan data: {ex}");
                currentScanData = null;
            }
        }

        private static void SaveCurrentScanData()
        {
            try
            {
                string directoryPath = Path.GetDirectoryName(scanOutputPath);
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }
                var json = JsonConvert.SerializeObject(currentScanData);
                File.WriteAllText(scanOutputPath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error saving scan data: {ex}");
            }
        }

        static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
        {

            var csFiles = importedAssets
                .Concat(movedAssets)
                .Where(path => path.EndsWith(".cs"))
                .ToArray();

            var deletedFiles = deletedAssets
                .Concat(movedFromAssetPaths)
                .Where(path => path.EndsWith(".cs"))
                .ToArray();

            if (csFiles.Length > 0 || deletedFiles.Length > 0)
            {
                ProcessChangedFiles(csFiles, deletedFiles);
            }

            foreach (var asset in importedAssets)
            {
                if (asset == CacheFilePath) continue; // Ignore cache file
                AddOrUpdateAsset(asset);
            }

            foreach (var asset in deletedAssets)
            {
                if (asset == CacheFilePath) continue; // Ignore cache file
                RemoveAsset(asset);
            }

            for (int i = 0; i < movedAssets.Length; i++)
            {
                if (movedAssets[i] == CacheFilePath || movedFromAssetPaths[i] == CacheFilePath) continue;
                RemoveAsset(movedFromAssetPaths[i]);
                AddOrUpdateAsset(movedAssets[i]);
            }
        }



    }
}