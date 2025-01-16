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
    internal class IndieBuff_ContextGraphBuilder
    {
        private Dictionary<string, object> contextData;
        private bool isProcessing = false;
        private Queue<GameObject> objectsToProcess;
        private HashSet<UnityEngine.Object> processedObjects = new HashSet<UnityEngine.Object>();
        private Dictionary<GameObject, GameObject> prefabContentsMap = new Dictionary<GameObject, GameObject>();
        private List<GameObject> loadedPrefabContents = new List<GameObject>();
        private List<UnityEngine.Object> _contextObjects;
        private const int MAX_CHILDREN_PER_FRAME = 10;
        private HashSet<long> m_VisitedObjects = new HashSet<long>();
        private HashSet<long> m_VisitedNodes = new HashSet<long>();
        private int m_MaxObjectDepth = -1;
        private int m_CurrentDepth;
        private Stack<int> m_Depths = new Stack<int>();
        private bool IgnorePrefabInstance = false;
        private bool UseDisplayName = false;
        private bool OutputType = false;
        private int m_ObjectDepth = 0;
        private int _maxTokenCount;
        private int currentTokenCount = 0;
        private TaskCompletionSource<Dictionary<string, object>> _completionSource;
        private bool includeConsoleLogs;

        public IndieBuff_ContextGraphBuilder(List<UnityEngine.Object> contextObjects, int maxTokenCount = int.MaxValue, bool includeConsoleLogs = false)
        {
            _contextObjects = contextObjects;
            _maxTokenCount = maxTokenCount;
            this.includeConsoleLogs = includeConsoleLogs;
        }

        internal Task<Dictionary<string, object>> StartContextBuild()
        {
            _completionSource = new TaskCompletionSource<Dictionary<string, object>>();
            isProcessing = true;
            contextData = new Dictionary<string, object>();
            
            // so scene doesnt take logs
            if(includeConsoleLogs)
            {
                AddConsoleLogsToContext();
            }
            
            objectsToProcess = new Queue<GameObject>();
            processedObjects.Clear();
            prefabContentsMap.Clear();
            loadedPrefabContents.Clear();

            foreach (var obj in _contextObjects)
            {
                if (currentTokenCount >= _maxTokenCount) break;
                if (obj is GameObject gameObject)
                {
                    if (PrefabUtility.IsPartOfPrefabAsset(gameObject))
                    {
                        string prefabPath = AssetDatabase.GetAssetPath(gameObject);

                        if (prefabPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                        {

                            GameObject fbxRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                            if (fbxRoot != null)
                            {
                                loadedPrefabContents.Add(fbxRoot);
                                prefabContentsMap[gameObject] = fbxRoot;
                            }
                            else
                            {
                                Debug.LogError($"Failed to load FBX asset at path: {fbxRoot}");
                            }
                        }
                        else if (prefabPath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                        {
                            // Load prefab contents if it's a valid prefab file
                            //GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                            GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                            if (prefabRoot != null)
                            {
                                loadedPrefabContents.Add(prefabRoot);
                                prefabContentsMap[gameObject] = prefabRoot;
                            }
                            else
                            {
                                Debug.LogError($"Failed to load prefab contents at path: {prefabPath}");
                            }
                        }

                        objectsToProcess.Enqueue(gameObject);
                    }
                    else
                    {
                        objectsToProcess.Enqueue(gameObject);
                    }
                }
                else
                {
                    ProcessGenericUnityObject(obj);
                }
            }

            EditorApplication.update += ProcessObjectsQueue;
            return _completionSource.Task;
        }

        private int EstimateTokenCount(object obj)
        {
            try
            {
                string jsonString = JsonConvert.SerializeObject(obj);
                return (int)Math.Ceiling(jsonString.Length / 4.0);
            }
            catch (Exception)
            {
                Debug.LogError("something wrong in estimation");
                return 2;
            }
        }

        private void AddToContext(string key, object value)
        {
            int newItemTokenCount = EstimateTokenCount(value);

            if (currentTokenCount + newItemTokenCount > _maxTokenCount)
            {
                currentTokenCount = _maxTokenCount;
                return;
            }

            contextData[key] = value;
            currentTokenCount += newItemTokenCount;
        }


        private void ProcessGenericUnityObject(UnityEngine.Object obj)
        {
            if (obj == null || processedObjects.Contains(obj)) return;

            try
            {
                processedObjects.Add(obj);
                var objectData = new Dictionary<string, object>
                {
                    ["type"] = obj.GetType().Name,
                    ["name"] = obj.name,
                    ["instance_id"] = obj.GetInstanceID(),
                    ["properties"] = GetSerializedProperties(obj),
                    ["assetPath"] = AssetDatabase.GetAssetPath(obj)
                };

                if (obj is ScriptableObject scriptableObj)
                {
                    // Get the script content if it's a MonoScript
                    if (obj is MonoScript monoScript)
                    {
                        var scriptPath = AssetDatabase.GetAssetPath(monoScript);

                        if (!string.IsNullOrEmpty(scriptPath))
                        {

                            objectData["type"] = "MonoScript";
                            objectData["scriptPath"] = scriptPath;
                            objectData["scriptContent"] = File.ReadAllLines(scriptPath);
                        }
                    }
                }
                else if (obj is MonoScript monoScript)
                {
                    var scriptPath = AssetDatabase.GetAssetPath(monoScript);
                    if (!string.IsNullOrEmpty(scriptPath))
                    {

                        objectData["type"] = "MonoScript";
                        objectData["scriptPath"] = scriptPath;
                        objectData["scriptContent"] = File.ReadAllLines(scriptPath);
                    }
                }
                else if (obj is UnityEditor.Animations.AnimatorController animator)
                {
                    objectData["properties"] = GetAnimatorControllerProperties(animator);
                }
                // check if its a material. Have to do this differently from animator because animator properties are empty. adding proeprties to material
                else if (obj is Material material)
                {
                    // Add material-specific properties to the existing properties dictionary so ai will be able to focus on it
                    var oldProperties = (Dictionary<string, object>)objectData["properties"];
                    var materialProperties = GetMaterialProperties(material);

                    var newProperties = new Dictionary<string, object>(materialProperties);

                    foreach (var kvp in oldProperties)
                    {
                        newProperties[kvp.Key] = kvp.Value;
                    }

                    objectData["properties"] = newProperties;
                }

                AddToContext(obj.name, objectData);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error processing Unity object {obj.name}: {e.Message}");
            }
        }
        private void ProcessObjectsQueue()
        {
            if (!isProcessing || objectsToProcess == null)
            {
                CompleteProcessing();
                return;
            }

            try
            {
                int processedThisFrame = 0;
                while (objectsToProcess.Count > 0 && processedThisFrame < MAX_CHILDREN_PER_FRAME)
                {
                    var gameObject = objectsToProcess.Dequeue();
                    if (gameObject != null && !processedObjects.Contains(gameObject))
                    {
                        ProcessGameObject(gameObject);
                    }
                    processedThisFrame++;
                }

                if (objectsToProcess.Count == 0)
                {
                    CompleteProcessing();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error in ProcessObjectsQueue: {e.Message}\n{e.StackTrace}");
                CompleteProcessing();
            }
        }


        private void CompleteProcessing()
        {
            isProcessing = false;
            EditorApplication.update -= ProcessObjectsQueue;

            try
            {
                // Unload all prefab contents
                foreach (var prefabContent in loadedPrefabContents)
                {
                    if (prefabContent != null)
                    {
                        string prefabPath = AssetDatabase.GetAssetPath(prefabContent);
                        if (prefabPath.EndsWith(".PREFAB", System.StringComparison.OrdinalIgnoreCase))
                        {
                            // logic to unload
                        }

                    }
                }
                _completionSource?.TrySetResult(contextData);

            }
            catch (Exception e)
            {
                Debug.LogError($"Error completing context processing: {e.Message}\n{e.StackTrace}");
            }
            finally
            {
                processedObjects.Clear();
                prefabContentsMap.Clear();
                loadedPrefabContents.Clear();
            }
        }

        private void OnDestroy()
        {

            foreach (var prefabContent in loadedPrefabContents)
            {
                if (prefabContent != null)
                {
                    //PrefabUtility.UnloadPrefabContents(prefabContent);
                }
            }
        }


        private void ProcessGameObject(GameObject gameObject)
        {
            if (gameObject == null || processedObjects.Contains(gameObject)) return;

            try
            {
                processedObjects.Add(gameObject);

                bool isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(gameObject);
                bool isPrefabInstance = PrefabUtility.IsPartOfPrefabInstance(gameObject);

                // If this is a prefab asset, check if we have loaded contents for it
                GameObject objectToProcess = gameObject;
                if (isPrefabAsset && prefabContentsMap.ContainsKey(gameObject))
                {
                    objectToProcess = prefabContentsMap[gameObject];
                }

                string key = GetUniqueGameObjectKey(objectToProcess);

                var gameObjectData = new Dictionary<string, object>
                {
                    ["type"] = "GameObject",
                    ["name"] = objectToProcess.name,
                    ["hierarchy_path"] = key,
                    ["parent"] = objectToProcess.transform.parent != null ? objectToProcess.transform.parent.gameObject.name : "null",
                    ["tag"] = objectToProcess.tag,
                    ["layer"] = LayerMask.LayerToName(objectToProcess.layer),
                    ["active"] = objectToProcess.activeSelf,
                    ["isPrefabAsset"] = isPrefabAsset,
                    ["isPrefabInstance"] = isPrefabInstance,
                    ["instance_id"] = objectToProcess.GetInstanceID(),
                    ["components"] = GetComponentsData(objectToProcess)
                };

                if (isPrefabAsset)
                {
                    gameObjectData["prefabAssetPath"] = AssetDatabase.GetAssetPath(gameObject);
                }
                else if (isPrefabInstance)
                {
                    var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
                    if (prefabAsset != null)
                    {
                        gameObjectData["prefabAssetPath"] = AssetDatabase.GetAssetPath(prefabAsset);
                        gameObjectData["prefabAssetName"] = prefabAsset.name;
                    }
                }


                AddToContext(key, gameObjectData);


                // Process children
                Transform transform = objectToProcess.transform;
                if (transform != null && transform.childCount > 0)
                {
                    var children = new List<GameObject>();
                    for (int i = 0; i < transform.childCount; i++)
                    {
                        Transform childTransform = transform.GetChild(i);
                        if (childTransform != null)
                        {
                            GameObject child = childTransform.gameObject;
                            if (child != null && !processedObjects.Contains(child))
                            {
                                children.Add(child);
                            }
                        }
                    }

                    gameObjectData["childCount"] = children.Count;
                    gameObjectData["children"] = children.Select(c => c.name).ToList();

                    foreach (var child in children)
                    {
                        if (objectsToProcess.Count < 10000)
                        {
                            objectsToProcess.Enqueue(child);
                        }
                        else
                        {
                            Debug.LogWarning($"Queue limit reached. Skipping remaining children of {objectToProcess.name}");
                            break;
                        }
                    }
                }
                else
                {
                    gameObjectData["childCount"] = 0;
                    gameObjectData["children"] = new List<string>();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error processing GameObject {gameObject.name}: {e.Message}\n{e.StackTrace}");
            }
        }

        private string GetUniqueGameObjectKey(GameObject obj)
        {
            // Create a unique key that includes hierarchy path to prevent naming conflicts
            string path = obj.name;
            Transform current = obj.transform;
            while (current.parent != null)
            {
                current = current.parent;
                path = current.name + "/" + path;
            }
            return path;
        }

        private Dictionary<string, object> GetComponentsData(GameObject gameObject)
        {
            var componentsData = new Dictionary<string, object>();
            var components = gameObject.GetComponents<Component>();

            foreach (var component in components.Where(c => c != null))
            {
                try
                {
                    var componentData = new Dictionary<string, object>
                    {
                        ["type"] = component.GetType().Name
                    };

                    // Handle MonoBehaviour scripts
                    if (component is MonoBehaviour script)
                    {
                        var scriptData = ProcessMonoBehaviourScript(script);
                        foreach (var kvp in scriptData)
                        {
                            componentData[kvp.Key] = kvp.Value;
                        }
                    }
                    else if (component is Animator animator)
                    {
                        // Custom handling for Animator component
                        componentData["properties"] = GetAnimatorProperties(animator);
                    }
                    else
                    {
                        // Handle built-in components
                        componentData["properties"] = GetSerializedProperties(component);
                    }

                    componentsData[component.GetType().Name] = componentData;
                }
                catch (Exception)
                {
                    //Debug.LogWarning($"Skipped component {component.GetType().Name}: {e.Message}");
                }
            }

            return componentsData;
        }

        private Dictionary<string, object> ProcessMonoBehaviourScript(MonoBehaviour script)
        {
            var scriptData = new Dictionary<string, object>();

            try
            {
                var monoScript = MonoScript.FromMonoBehaviour(script);
                var scriptPath = AssetDatabase.GetAssetPath(monoScript);

                if (!string.IsNullOrEmpty(scriptPath))
                {
                    
                    if (!scriptPath.Contains("Packages/com.unity.ugui/Runtime/UI/"))
                    {
                        scriptData["scriptPath"] = scriptPath;
                        scriptData["scriptContent"] = File.ReadAllLines(scriptPath);
                        scriptData["type"] = "MonoScript";
                    }
                    // if its a ui element, get the serialized properties
                    else{
                        scriptData["properties"] = GetSerializedProperties(script);
                    }
                }

                /*
                // Get custom attributes
                var attributes = script.GetType()
                    .GetCustomAttributes(true)
                    .Select(attr => attr.GetType().Name)
                    .ToList();

                if (attributes.Any())
                {
                    //scriptData["attributes"] = attributes;
                }*/
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Error processing MonoBehaviour {script.name}: {e.Message}");
            }

            return scriptData;
        }


        private Dictionary<string, object> GetSerializedProperties(object obj)
        {
            var properties = new Dictionary<string, object>();
            var serializedObject = new SerializedObject(obj as UnityEngine.Object);
            var iterator = serializedObject.GetIterator();

            while (iterator.NextVisible(true))
            {
                try
                {
                    ProcessSerializedPropertyInner(properties, iterator);
                }
                catch (Exception)
                {
                    // Skip problematic properties
                }
            }

            return properties;
        }

        private void ProcessSerializedPropertyInner(Dictionary<string, object> properties, SerializedProperty current)
        {
            // THIS MIGHT BE NEEDED. prevents infinite loop i think but I removed it and its working. Commented out for now because was blocking some properties from being processed.
            /*if (current.depth < m_CurrentDepth)
            {
                Debug.Log($"Skipping {current.name} due to depth check");
                return;
            }*/

            if (current.propertyType == SerializedPropertyType.ManagedReference && m_VisitedNodes.Contains(current.managedReferenceId))
            {
                return;
            }

            if (current.name == "m_PrefabInstance" && IgnorePrefabInstance)
            {
                return;
            }

            var key = UseDisplayName ? current.displayName : current.name;
            var type = current.propertyType.ToString();

            if (current.propertyType == SerializedPropertyType.Generic && current.isArray)
            {
                type = $"Array({PrettifyString(current.arrayElementType)})";
            }
            if (current.propertyType == SerializedPropertyType.ObjectReference || current.propertyType == SerializedPropertyType.ExposedReference)
            {
                if (current.objectReferenceValue != null)
                {
                    type = current.objectReferenceValue.GetType().Name;
                }
                else
                {
                    type = PrettifyString(current.type);
                }
            }

            if (OutputType)
                key += $" - {type}";

            // Override for GameObject's component list
            if (type == "Array(ComponentPair)")
                key = "Components";

            m_CurrentDepth++;

            switch (current.propertyType)
            {
                case SerializedPropertyType.Generic:
                    {
                        if (current.isArray)
                        {
                            var arrayValues = new Dictionary<string, object>();
                            //var arrayValues = new List<object>();
                            var length = current.arraySize;
                            for (var i = 0; i < length; i++)
                            {
                                var arrayElement = current.GetArrayElementAtIndex(i);
                                ProcessSerializedPropertyInner(arrayValues, arrayElement);
                            }
                            properties[key] = arrayValues;
                        }
                        else
                        {
                            if (current.hasChildren)
                            {
                                var childProp = current.Copy();
                                childProp.Next(true);
                                var childProperties = new Dictionary<string, object>();
                                ProcessSerializedPropertyInner(childProperties, childProp);
                                properties[key] = childProperties;
                            }
                            else
                                properties[key] = "Generic no children";
                        }
                    }
                    break;
                case SerializedPropertyType.Integer:
                    properties[key] = current.intValue;
                    break;
                case SerializedPropertyType.Boolean:
                    properties[key] = current.boolValue;
                    break;
                case SerializedPropertyType.Float:
                    properties[key] = SafeNumberWrite(current.floatValue);
                    break;
                case SerializedPropertyType.String:
                    properties[key] = current.stringValue;
                    break;
                case SerializedPropertyType.Color:
                    properties[key] = current.colorValue.ToString();
                    break;
                case SerializedPropertyType.ObjectReference:
                    {
                        var objectReference = current.objectReferenceValue;
                        if (objectReference != null)
                        {
                            var instanceID = objectReference.GetInstanceID();
                            if (!m_VisitedObjects.Contains(instanceID))
                            {
                                if (m_MaxObjectDepth > -1 && m_ObjectDepth > m_MaxObjectDepth)
                                {
                                    properties[key] = $"{objectReference.name}";
                                }
                                else
                                {
                                    m_Depths.Push(m_CurrentDepth);
                                    var SO = new SerializedObject(objectReference);
                                    var childProperties = new Dictionary<string, object>();
                                    ProcessSerializedPropertyInner(childProperties, SO.GetIterator());
                                    properties[key] = childProperties;
                                    m_CurrentDepth = m_Depths.Pop();
                                }
                            }
                            else
                            {
                                properties[key] = $"Already serialized - {objectReference.name}";
                            }
                        }
                        else
                        {
                            properties[key] = "null";
                        }
                    }
                    break;
                case SerializedPropertyType.LayerMask:
                    properties[key] = current.intValue;
                    break;
                case SerializedPropertyType.Enum:
                    {
                        if (current.enumValueIndex >= 0 && current.enumValueIndex < current.enumDisplayNames.Length)
                        {
                            properties[key] = current.enumDisplayNames[current.enumValueIndex];
                        }
                        else
                        {
                            properties[key] = current.enumValueFlag;
                        }
                    }
                    break;
                case SerializedPropertyType.Vector2:
                    properties[key] = current.vector2Value.ToString();
                    break;
                case SerializedPropertyType.Vector3:
                    properties[key] = current.vector3Value.ToString();
                    break;
                case SerializedPropertyType.Vector4:
                    properties[key] = current.vector4Value.ToString();
                    break;
                case SerializedPropertyType.Rect:
                    properties[key] = current.rectValue.ToString();
                    break;
                case SerializedPropertyType.ArraySize:
                    properties[key] = current.intValue;
                    break;
                case SerializedPropertyType.Character:
                    properties[key] = $"Character - {current.boxedValue}";
                    break;
                case SerializedPropertyType.AnimationCurve:
                    properties[key] = $"Animation curve - {current.animationCurveValue}";
                    break;
                case SerializedPropertyType.Bounds:
                    properties[key] = $"{current.boundsValue}";
                    break;
                case SerializedPropertyType.Gradient:
                    properties[key] = $"Gradient - {current.gradientValue}";
                    break;
                case SerializedPropertyType.Quaternion:
                    properties[key] = current.quaternionValue.ToString();
                    break;
                case SerializedPropertyType.ExposedReference:
                    {
                        var objectReference = current.objectReferenceValue;
                        if (objectReference != null)
                        {
                            var instanceID = objectReference.GetInstanceID();
                            if (!m_VisitedObjects.Contains(instanceID))
                            {
                                if (m_MaxObjectDepth > -1 && m_ObjectDepth > m_MaxObjectDepth)
                                {
                                    properties[key] = $"{objectReference.name}";
                                }
                                else
                                {
                                    m_Depths.Push(m_CurrentDepth);
                                    var SO = new SerializedObject(objectReference);
                                    var childProperties = new Dictionary<string, object>();
                                    ProcessSerializedPropertyInner(childProperties, SO.GetIterator());
                                    properties[key] = childProperties;
                                    m_CurrentDepth = m_Depths.Pop();
                                }
                            }
                            else
                            {
                                properties[key] = $"Already serialized  - {objectReference.name}";
                            }
                        }
                        else
                        {
                            properties[key] = "null";
                        }
                    }
                    break;
                case SerializedPropertyType.FixedBufferSize:
                    properties[key] = current.intValue;
                    break;
                case SerializedPropertyType.Vector2Int:
                    properties[key] = current.vector2IntValue.ToString();
                    break;
                case SerializedPropertyType.Vector3Int:
                    properties[key] = current.vector3IntValue.ToString();
                    break;
                case SerializedPropertyType.RectInt:
                    properties[key] = current.rectIntValue.ToString();
                    break;
                case SerializedPropertyType.BoundsInt:
                    properties[key] = current.boundsIntValue.ToString();
                    break;
                case SerializedPropertyType.ManagedReference:
                    {
                        var refId = current.managedReferenceId;
                        var visited = false;

                        if (!m_VisitedNodes.Contains(refId))
                        {
                            m_VisitedNodes.Add(current.managedReferenceId);
                            if (current.hasChildren)
                            {
                                visited = true;
                                var childProp = current.Copy();
                                childProp.Next(true);
                                var childProperties = new Dictionary<string, object>();
                                ProcessSerializedPropertyInner(childProperties, childProp);
                                properties[key] = childProperties;
                            }
                        }

                        if (!visited)
                        {
                            var boxedValue = current.boxedValue;
                            properties[key] = $"Managed reference ID: {boxedValue}";
                        }

                    }
                    break;
                case SerializedPropertyType.Hash128:
                    properties[key] = current.hash128Value.ToString();
                    break;
                default:
                    properties[key] = $"unsupported - {current.propertyType}";
                    break;
            }

            m_CurrentDepth--;
        }

        private static string SafeNumberWrite(float value)
        {
            if (float.IsFinite(value))
                return value.ToString();
            else
                return value.ToString();
        }

        private static string PrettifyString(string toPrettify)
        {
            if (toPrettify.StartsWith("PPtr<"))
                return toPrettify.Substring(5, toPrettify.Length - 6);

            return toPrettify;
        }

        private bool IsSerializableValue(object value)
        {
            if (value == null) return false;
            var type = value.GetType();

            return type.IsPrimitive ||
                   type.IsEnum ||
                   type == typeof(string) ||
                   type == typeof(Vector2) ||
                   type == typeof(Vector3) ||
                   type == typeof(Vector4) ||
                   type == typeof(Quaternion) ||
                   type == typeof(Color) ||
                   type == typeof(LayerMask) ||
                   type == typeof(AnimationCurve) ||
                   (type.IsArray && IsSerializableValue(type.GetElementType()));
        }

        
        private Dictionary<string, object> GetAnimatorProperties(Animator animator)
        {
            var properties = new Dictionary<string, object>();


            var animatorController = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;


            // Extract parameters
            var parameterList = animatorController.parameters.Select(parameter => new Dictionary<string, object>
            {
                ["name"] = parameter.name,
                ["type"] = parameter.type.ToString(),
                ["defaultFloat"] = parameter.defaultFloat,
                ["defaultInt"] = parameter.defaultInt,
                ["defaultBool"] = parameter.defaultBool
            }).ToList();

            properties["parameters"] = parameterList;

            // Extract states and transitions
            var stateMachines = animatorController.layers.Select(layer => layer.stateMachine).ToList();
            var statesList = new List<Dictionary<string, object>>();
            var exitTransitions = new List<Dictionary<string, object>>();

            foreach (var stateMachine in stateMachines)
            {
                // Handle entry node
                var entryTransitions = stateMachine.entryTransitions
                    .Where(t => t.destinationState != null)
                    .Select(t => new Dictionary<string, object>
                    {
                        ["name"] = t.destinationState.name
                    }).ToList();

                statesList.Add(new Dictionary<string, object>
                {
                    ["name"] = "Entry",
                    ["type"] = "EntryNode",
                    ["transitions"] = entryTransitions
                });

                // Process all regular states and collect exit transitions
                foreach (var state in stateMachine.states)
                {
                    var stateTransitions = new List<Dictionary<string, object>>();

                    foreach (var transition in state.state.transitions)
                    {
                        // Check if it's an exit transition
                        if (transition.destinationState == null &&
                            transition.destinationStateMachine == null &&
                            transition.isExit)
                        {
                            // Add to state's transitions without sourceState
                            stateTransitions.Add(new Dictionary<string, object>
                            {
                                ["name"] = "Exit",
                                ["duration"] = transition.duration,
                                ["offset"] = transition.offset,
                                ["hasExitTime"] = transition.hasExitTime,
                                ["exitTime"] = transition.exitTime
                            });

                            // Add to exit transitions list with sourceState
                            exitTransitions.Add(new Dictionary<string, object>
                            {
                                ["sourceState"] = state.state.name,
                                ["duration"] = transition.duration,
                                ["offset"] = transition.offset,
                                ["hasExitTime"] = transition.hasExitTime,
                                ["exitTime"] = transition.exitTime
                            });
                        }
                        // Regular transition to another state
                        else if (transition.destinationState != null)
                        {
                            stateTransitions.Add(new Dictionary<string, object>
                            {
                                ["name"] = transition.destinationState.name,
                                ["duration"] = transition.duration,
                                ["offset"] = transition.offset,
                                ["hasExitTime"] = transition.hasExitTime,
                                ["exitTime"] = transition.exitTime
                            });
                        }
                    }

                    statesList.Add(new Dictionary<string, object>
                    {
                        ["name"] = state.state.name,
                        ["speed"] = state.state.speed,
                        ["tag"] = state.state.tag,
                        ["transitions"] = stateTransitions
                    });
                }

                // Handle exit node with collected transitions
                statesList.Add(new Dictionary<string, object>
                {
                    ["name"] = "Exit",
                    ["type"] = "ExitNode",
                    ["incomingTransitions"] = exitTransitions,
                    ["transitions"] = new List<Dictionary<string, object>>()
                });

                // Add any state transitions only if they exist
                var anyStateTransitions = stateMachine.anyStateTransitions
                    .Where(t => t.destinationState != null)
                    .Select(t => new Dictionary<string, object>
                    {
                        ["name"] = t.destinationState.name,
                        ["duration"] = t.duration,
                        ["offset"] = t.offset,
                        ["hasExitTime"] = t.hasExitTime,
                        ["exitTime"] = t.exitTime
                    }).ToList();

                if (anyStateTransitions.Any())
                {
                    properties["transitions"] = anyStateTransitions;
                }
            }

            properties["states"] = statesList;

            return properties;
        }

        private Dictionary<string, object> GetAnimatorControllerProperties(UnityEditor.Animations.AnimatorController animatorController)
        {
            var properties = new Dictionary<string, object>();

            // Extract parameters
            var parameterList = animatorController.parameters.Select(parameter => new Dictionary<string, object>
            {
                ["name"] = parameter.name,
                ["type"] = parameter.type.ToString(),
                ["defaultFloat"] = parameter.defaultFloat,
                ["defaultInt"] = parameter.defaultInt,
                ["defaultBool"] = parameter.defaultBool
            }).ToList();

            properties["parameters"] = parameterList;

            // Extract states and transitions
            var stateMachines = animatorController.layers.Select(layer => layer.stateMachine).ToList();
            var statesList = new List<Dictionary<string, object>>();
            var exitTransitions = new List<Dictionary<string, object>>();

            foreach (var stateMachine in stateMachines)
            {
                // Handle entry node
                var entryTransitions = stateMachine.entryTransitions
                    .Where(t => t.destinationState != null)
                    .Select(t => new Dictionary<string, object>
                    {
                        ["name"] = t.destinationState.name
                    }).ToList();

                statesList.Add(new Dictionary<string, object>
                {
                    ["name"] = "Entry",
                    ["type"] = "EntryNode",
                    ["transitions"] = entryTransitions
                });

                // Process all regular states and collect exit transitions
                foreach (var state in stateMachine.states)
                {
                    var stateTransitions = new List<Dictionary<string, object>>();

                    foreach (var transition in state.state.transitions)
                    {
                        // Check if it's an exit transition
                        if (transition.destinationState == null &&
                            transition.destinationStateMachine == null &&
                            transition.isExit)
                        {
                            // Add to state's transitions without sourceState
                            stateTransitions.Add(new Dictionary<string, object>
                            {
                                ["name"] = "Exit",
                                ["duration"] = transition.duration,
                                ["offset"] = transition.offset,
                                ["hasExitTime"] = transition.hasExitTime,
                                ["exitTime"] = transition.exitTime
                            });

                            // Add to exit transitions list with sourceState
                            exitTransitions.Add(new Dictionary<string, object>
                            {
                                ["sourceState"] = state.state.name,
                                ["duration"] = transition.duration,
                                ["offset"] = transition.offset,
                                ["hasExitTime"] = transition.hasExitTime,
                                ["exitTime"] = transition.exitTime
                            });
                        }
                        // Regular transition to another state
                        else if (transition.destinationState != null)
                        {
                            stateTransitions.Add(new Dictionary<string, object>
                            {
                                ["name"] = transition.destinationState.name,
                                ["duration"] = transition.duration,
                                ["offset"] = transition.offset,
                                ["hasExitTime"] = transition.hasExitTime,
                                ["exitTime"] = transition.exitTime
                            });
                        }
                    }

                    statesList.Add(new Dictionary<string, object>
                    {
                        ["name"] = state.state.name,
                        ["speed"] = state.state.speed,
                        ["tag"] = state.state.tag,
                        ["transitions"] = stateTransitions
                    });
                }

                // Handle exit node with collected transitions
                statesList.Add(new Dictionary<string, object>
                {
                    ["name"] = "Exit",
                    ["type"] = "ExitNode",
                    ["incomingTransitions"] = exitTransitions,
                    ["transitions"] = new List<Dictionary<string, object>>()
                });

                // Add any state transitions only if they exist
                var anyStateTransitions = stateMachine.anyStateTransitions
                    .Where(t => t.destinationState != null)
                    .Select(t => new Dictionary<string, object>
                    {
                        ["name"] = t.destinationState.name,
                        ["duration"] = t.duration,
                        ["offset"] = t.offset,
                        ["hasExitTime"] = t.hasExitTime,
                        ["exitTime"] = t.exitTime
                    }).ToList();

                if (anyStateTransitions.Any())
                {
                    properties["transitions"] = anyStateTransitions;
                }
            }

            properties["states"] = statesList;

            return properties;
        }

        private Dictionary<string, object> GetMaterialProperties(Material material)
        {
            var properties = new Dictionary<string, object>();
            
            try
            {
                // Get the main color as a string to avoid serialization issues
                Color mainColor = material.color;
                properties["color"] = $"({mainColor.r:F3}, {mainColor.g:F3}, {mainColor.b:F3}, {mainColor.a:F3})";
                
                // Add shader name
                properties["shader"] = material.shader != null ? material.shader.name : "null";
                
                // Add whether the material is transparent
                properties["isTransparent"] = material.GetTag("RenderType", false) == "Transparent";
            }
            catch (Exception e)
            {
                Debug.LogError($"Error getting material properties: {e.Message}");
            }
            
            return properties;
        }

        private void AddConsoleLogsToContext()
        {
            try
            {
                var consoleData = new Dictionary<string, object>();
                var consoleLogs = IndieBuff_UserSelectedContext.Instance.ConsoleLogs;
                
                if (consoleLogs.Count > 0)
                {
                    var logEntries = consoleLogs.Select(log => new Dictionary<string, object>
                    {
                        // if message is more then 200 characters, truncate it
                        ["message"] = log.Message.Length > 200 ? log.Message.Substring(0, 200) : log.Message,
                        ["file"] = log.File,
                        ["line"] = log.Line,
                        ["column"] = log.Column,
                        ["mode"] = log.Mode.ToString()
                    }).ToList();
                    
                    consoleData["logs"] = logEntries;
                    AddToContext("console_logs", consoleData);
                }
                else{
                    // pass in an empty dictionary
                    consoleData["logs"] = new List<Dictionary<string, object>>();
                    AddToContext("console_logs", consoleData);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error adding console logs to context: {e.Message}");
            }
        }
    }
}