using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace IndieBuff.Editor
{
    internal class IndieBuff_ContextStatePersistence
    {
        private const string CONTEXT_OBJECTS_KEY = "IndieBuff_SelectedContextObjects";
        private const string CONSOLE_LOGS_KEY = "IndieBuff_SelectedConsoleLogs";
        
        private readonly IndieBuff_UserSelectedContext _context;

        internal IndieBuff_ContextStatePersistence(IndieBuff_UserSelectedContext context)
        {
            _context = context;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnBeforeAssemblyReload()
        {
            SaveState();
        }

        private void OnAfterAssemblyReload()
        {
            RestoreStateIfNeeded();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                SaveState();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                RestoreStateIfNeeded();
            }
        }

        public bool IsObjectInContext(UnityEngine.Object obj)
        {
            return _context.UserContextObjects.Contains(obj);
        }

        public void SaveState()
        {
            // get objects instance ids
            var objectIds = _context.UserContextObjects.Where(obj => obj != null)
                .Select(obj => obj.GetInstanceID())
                .ToArray();
            
            // save objects instance ids
            string serializedIds = JsonConvert.SerializeObject(objectIds);
            SessionState.SetString(CONTEXT_OBJECTS_KEY, serializedIds);
            
            // save logs
            var serializedLogs = JsonConvert.SerializeObject(_context.ConsoleLogs);
            SessionState.SetString(CONSOLE_LOGS_KEY, serializedLogs);
        }

        public void RestoreStateIfNeeded()
        {
            string objectsJson = SessionState.GetString(CONTEXT_OBJECTS_KEY, "");
            string logsJson = SessionState.GetString(CONSOLE_LOGS_KEY, "");

            // if both json objects are empty just return
            if (string.IsNullOrEmpty(objectsJson) && string.IsNullOrEmpty(logsJson))
            {
                return;
            }
            
            try
            {
                // Restore objects
                if (!string.IsNullOrEmpty(objectsJson))
                {
                    int[] objectIds = JsonConvert.DeserializeObject<int[]>(objectsJson);
                    if (objectIds.Length > 0)
                    {
                        _context.ClearContextObjects();
                        foreach (int id in objectIds)
                        {
                            var obj = EditorUtility.InstanceIDToObject(id);
                            if (obj != null)
                            {
                                _context.AddContextObject(obj);
                            }
                        }
                    }
                }

                // Restore logs
                if (!string.IsNullOrEmpty(logsJson))
                {
                    _context.ConsoleLogs.Clear();
                    var logs = JsonConvert.DeserializeObject<List<IndieBuff_LogEntry>>(logsJson);
                    foreach (var log in logs)
                    {
                        _context.AddConsoleLog(log);
                    }
                }

                // Always notify of context update, even if nothing was restored
                _context.onUserSelectedContextUpdated?.Invoke();
                
                if (!string.IsNullOrEmpty(objectsJson) || !string.IsNullOrEmpty(logsJson))
                {
                    CleanupSessionState();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"IndieBuff_ContextStatePersistence: Error restoring state: {e}");
            }
        }

        private void CleanupSessionState()
        {
            SessionState.EraseString(CONTEXT_OBJECTS_KEY);
            SessionState.EraseString(CONSOLE_LOGS_KEY);
        }

        public void Cleanup()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            CleanupSessionState();
        }
    }
} 