#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using System;
using System.Linq;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;

[CustomEditor(typeof(LoadPrefabs))]
public class LoadPrefabsEditor : Editor
{
    public static LoadPrefabsEditor instance;

    static LoadPrefabsEditor()
    {
        EditorSceneManager.sceneSaving += EditorSceneManager_sceneSaving;
    }

    private static void EditorSceneManager_sceneSaving(UnityEngine.SceneManagement.Scene scene, string path)
    {
        instance.UpdateTransformData();
        instance.SaveData();
    }

    private LoadPrefabs script;
    // Static dictionary to maintain object references between editor sessions
    private static Dictionary<string, GameObject> editorInstances = new Dictionary<string, GameObject>();
    private bool showAvailableAssets = true;
    private bool showInstantiatedAssets = true;

    private void OnEnable()
    {
        script = (LoadPrefabs)target;
        // Clean up missing instances from the dictionary
        List<string> keysToRemove = new List<string>();
        foreach (var kvp in editorInstances)
        {
            if (kvp.Value == null)
                keysToRemove.Add(kvp.Key);
        }

        foreach (var key in keysToRemove)
            editorInstances.Remove(key);
        instance = this;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space(10);

        // Available assets foldout
        SerializedProperty availableAssetGuidsProp = serializedObject.FindProperty("availableAssetGuids");
        showAvailableAssets = EditorGUILayout.Foldout(showAvailableAssets, "Available Addressable Assets", true);
        if (showAvailableAssets)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(availableAssetGuidsProp, true);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(10);

        // Instantiated assets foldout 
        SerializedProperty instantiatedAssetsProp = serializedObject.FindProperty("instantiatedAssets");
        showInstantiatedAssets = EditorGUILayout.Foldout(showInstantiatedAssets, "Instantiated Assets", true);
        if (showInstantiatedAssets)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(instantiatedAssetsProp, true);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(10);

        // Action buttons
        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Scan Scene for Addressables", GUILayout.Height(30)))
        {
            ScanSceneForAddressables();
        }

        if (GUILayout.Button("Instantiate From Data", GUILayout.Height(30)))
        {
            InstantiateFromData();
        }

        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Update Transform Data", GUILayout.Height(30)))
        {
            UpdateTransformData();
        }

        if (GUILayout.Button("Save Data", GUILayout.Height(30)))
        {
            SaveData();
        }

        GUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        if (GUILayout.Button("Clear All Instances", GUILayout.Height(25)))
        {
            ClearAllInstances();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void ScanSceneForAddressables()
    {
        Debug.Log("Starting scan for addressable prefabs...");

        // Get all GameObjects in the scene
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        Debug.Log($"Found {allObjects.Length} total objects in scene");

        // Track which objects we've already processed to avoid duplicates
        HashSet<GameObject> processedObjects = new HashSet<GameObject>();
        List<AssetData> newInstantiatedAssets = new List<AssetData>();
        int addressableCount = 0;

        // First pass: identify all prefab instance roots
        List<GameObject> prefabInstanceRoots = new List<GameObject>();
        foreach (GameObject obj in allObjects)
        {
            // Check if this is a prefab instance root
            if (PrefabUtility.IsAnyPrefabInstanceRoot(obj))
            {
                prefabInstanceRoots.Add(obj);
            }
        }

        Debug.Log($"Found {prefabInstanceRoots.Count} prefab instance roots in scene");

        // Process only prefab instance roots
        foreach (GameObject obj in prefabInstanceRoots)
        {
            if (processedObjects.Contains(obj))
                continue;

            bool isAddressable = AddressableAssetUtility.IsAssetAddressable(obj);
            if (isAddressable)
            {
                addressableCount++;
                processedObjects.Add(obj);

                GameObject prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(obj) ?? obj;
                string assetGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefabAsset));
                Debug.Log($"Found addressable asset: {obj.name} with GUID: {assetGuid}");

                // Add to available assets if not already there
                SerializedProperty availableAssetGuidsProp = serializedObject.FindProperty("availableAssetGuids");
                bool found = false;
                for (int i = 0; i < availableAssetGuidsProp.arraySize; i++)
                {
                    SerializedProperty element = availableAssetGuidsProp.GetArrayElementAtIndex(i);
                    string existingGuid = element.stringValue;
                    if (existingGuid == assetGuid)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    int index = availableAssetGuidsProp.arraySize;
                    availableAssetGuidsProp.arraySize++;
                    availableAssetGuidsProp.GetArrayElementAtIndex(index).stringValue = assetGuid;
                }

                // Create asset data
                string uniqueID = Guid.NewGuid().ToString();
                AssetData data = new AssetData
                {
                    assetReferenceKey = assetGuid,
                    uniqueID = uniqueID
                };

                // Set position values individually
                Vector3 pos = obj.transform.position;
                data.posX = pos.x;
                data.posY = pos.y;
                data.posZ = pos.z;

                // Set rotation values individually
                Quaternion rot = obj.transform.rotation;
                data.rotX = rot.x;
                data.rotY = rot.y;
                data.rotZ = rot.z;
                data.rotW = rot.w;

                // Set scale values individually
                Vector3 scale = obj.transform.localScale;
                data.scaleX = scale.x;
                data.scaleY = scale.y;
                data.scaleZ = scale.z;

                newInstantiatedAssets.Add(data);
                editorInstances[uniqueID] = obj;
            }
        }

        Debug.Log($"Found {addressableCount} addressable objects in scene");

        // Update instantiatedAssets list
        SerializedProperty instantiatedAssetsProp = serializedObject.FindProperty("instantiatedAssets");
        instantiatedAssetsProp.ClearArray();
        for (int i = 0; i < newInstantiatedAssets.Count; i++)
        {
            instantiatedAssetsProp.arraySize++;
            SerializedProperty element = instantiatedAssetsProp.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("assetReferenceKey").stringValue = newInstantiatedAssets[i].assetReferenceKey;
            element.FindPropertyRelative("uniqueID").stringValue = newInstantiatedAssets[i].uniqueID;

            // Set position properties
            element.FindPropertyRelative("posX").floatValue = newInstantiatedAssets[i].posX;
            element.FindPropertyRelative("posY").floatValue = newInstantiatedAssets[i].posY;
            element.FindPropertyRelative("posZ").floatValue = newInstantiatedAssets[i].posZ;

            // Set rotation properties
            element.FindPropertyRelative("rotX").floatValue = newInstantiatedAssets[i].rotX;
            element.FindPropertyRelative("rotY").floatValue = newInstantiatedAssets[i].rotY;
            element.FindPropertyRelative("rotZ").floatValue = newInstantiatedAssets[i].rotZ;
            element.FindPropertyRelative("rotW").floatValue = newInstantiatedAssets[i].rotW;

            // Set scale properties
            element.FindPropertyRelative("scaleX").floatValue = newInstantiatedAssets[i].scaleX;
            element.FindPropertyRelative("scaleY").floatValue = newInstantiatedAssets[i].scaleY;
            element.FindPropertyRelative("scaleZ").floatValue = newInstantiatedAssets[i].scaleZ;
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }
    private void InstantiateFromData()
    {
        SerializedProperty instantiatedAssetsProp = serializedObject.FindProperty("instantiatedAssets");

        // Keep track of which IDs were found in the data
        HashSet<string> foundIDs = new HashSet<string>();

        for (int i = 0; i < instantiatedAssetsProp.arraySize; i++)
        {
            SerializedProperty element = instantiatedAssetsProp.GetArrayElementAtIndex(i);
            string assetKey = element.FindPropertyRelative("assetReferenceKey").stringValue;
            string uniqueID = element.FindPropertyRelative("uniqueID").stringValue;

            foundIDs.Add(uniqueID);

            // Check if we already have this instance
            if (editorInstances.ContainsKey(uniqueID) && editorInstances[uniqueID] != null)
            {
                // We already have this instance - just update its transform
                GameObject instance = editorInstances[uniqueID];

                // Get transform data
                float posX = element.FindPropertyRelative("posX").floatValue;
                float posY = element.FindPropertyRelative("posY").floatValue;
                float posZ = element.FindPropertyRelative("posZ").floatValue;
                Vector3 position = new Vector3(posX, posY, posZ);

                float rotX = element.FindPropertyRelative("rotX").floatValue;
                float rotY = element.FindPropertyRelative("rotY").floatValue;
                float rotZ = element.FindPropertyRelative("rotZ").floatValue;
                float rotW = element.FindPropertyRelative("rotW").floatValue;
                Quaternion rotation = new Quaternion(rotX, rotY, rotZ, rotW);

                float scaleX = element.FindPropertyRelative("scaleX").floatValue;
                float scaleY = element.FindPropertyRelative("scaleY").floatValue;
                float scaleZ = element.FindPropertyRelative("scaleZ").floatValue;
                Vector3 scale = new Vector3(scaleX, scaleY, scaleZ);

                // Update the transform
                instance.transform.position = position;
                instance.transform.rotation = rotation;
                instance.transform.localScale = scale;
            }
            else
            {
                // We need to create this instance
                string assetPath = AssetDatabase.GUIDToAssetPath(assetKey);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

                    if (prefab != null)
                    {
                        // Get transform data
                        float posX = element.FindPropertyRelative("posX").floatValue;
                        float posY = element.FindPropertyRelative("posY").floatValue;
                        float posZ = element.FindPropertyRelative("posZ").floatValue;
                        Vector3 position = new Vector3(posX, posY, posZ);

                        float rotX = element.FindPropertyRelative("rotX").floatValue;
                        float rotY = element.FindPropertyRelative("rotY").floatValue;
                        float rotZ = element.FindPropertyRelative("rotZ").floatValue;
                        float rotW = element.FindPropertyRelative("rotW").floatValue;
                        Quaternion rotation = new Quaternion(rotX, rotY, rotZ, rotW);

                        float scaleX = element.FindPropertyRelative("scaleX").floatValue;
                        float scaleY = element.FindPropertyRelative("scaleY").floatValue;
                        float scaleZ = element.FindPropertyRelative("scaleZ").floatValue;
                        Vector3 scale = new Vector3(scaleX, scaleY, scaleZ);

                        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                        instance.transform.position = position;
                        instance.transform.rotation = rotation;
                        instance.transform.localScale = scale;
                        editorInstances[uniqueID] = instance;
                    }
                }
            }
        }

        // Remove any instances that weren't in the data
        List<string> keysToRemove = new List<string>();
        foreach (var kvp in editorInstances)
        {
            if (!foundIDs.Contains(kvp.Key) && kvp.Value != null)
            {
                DestroyImmediate(kvp.Value);
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
            editorInstances.Remove(key);
    }

    private void UpdateTransformData()
    {
        SerializedProperty instantiatedAssetsProp = serializedObject.FindProperty("instantiatedAssets");

        for (int i = 0; i < instantiatedAssetsProp.arraySize; i++)
        {
            SerializedProperty element = instantiatedAssetsProp.GetArrayElementAtIndex(i);
            string uniqueID = element.FindPropertyRelative("uniqueID").stringValue;

            if (editorInstances.ContainsKey(uniqueID) && editorInstances[uniqueID] != null)
            {
                GameObject instance = editorInstances[uniqueID];

                // Update position
                Vector3 pos = instance.transform.position;
                element.FindPropertyRelative("posX").floatValue = pos.x;
                element.FindPropertyRelative("posY").floatValue = pos.y;
                element.FindPropertyRelative("posZ").floatValue = pos.z;

                // Update rotation
                Quaternion rot = instance.transform.rotation;
                element.FindPropertyRelative("rotX").floatValue = rot.x;
                element.FindPropertyRelative("rotY").floatValue = rot.y;
                element.FindPropertyRelative("rotZ").floatValue = rot.z;
                element.FindPropertyRelative("rotW").floatValue = rot.w;

                // Update scale
                Vector3 scale = instance.transform.localScale;
                element.FindPropertyRelative("scaleX").floatValue = scale.x;
                element.FindPropertyRelative("scaleY").floatValue = scale.y;
                element.FindPropertyRelative("scaleZ").floatValue = scale.z;
            }
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }

    private void SaveData()
    {
        script.SaveAssetsData();
    }

    private void ClearAllInstances()
    {
        // Only destroy the game objects, but keep the references in the dictionary
        foreach (var kvp in editorInstances)
        {
            if (kvp.Value != null)
                DestroyImmediate(kvp.Value);
        }
    }

    // Draw gizmos to visualize the addressable instances
    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    private static void DrawGizmos(LoadPrefabs loadPrefabs, GizmoType gizmoType)
    {
        if (!EditorApplication.isPlaying)
        {
            // Draw indicators for uninstantiated entries
            var instantiatedAssetsProp = new SerializedObject(loadPrefabs).FindProperty("instantiatedAssets");
            if (instantiatedAssetsProp != null)
            {
                for (int i = 0; i < instantiatedAssetsProp.arraySize; i++)
                {
                    var element = instantiatedAssetsProp.GetArrayElementAtIndex(i);
                    string uniqueID = element.FindPropertyRelative("uniqueID").stringValue;

                    if (!editorInstances.ContainsKey(uniqueID) || editorInstances[uniqueID] == null)
                    {
                        // Draw a wireframe cube to indicate a missing instance
                        float posX = element.FindPropertyRelative("posX").floatValue;
                        float posY = element.FindPropertyRelative("posY").floatValue;
                        float posZ = element.FindPropertyRelative("posZ").floatValue;
                        Vector3 position = new Vector3(posX, posY, posZ);

                        Gizmos.color = Color.yellow;
                        Gizmos.DrawWireCube(position, Vector3.one * 0.3f);
                    }
                }
            }
        }
    }
}

public static class AddressableAssetUtility
{
    public static bool IsAssetAddressable(GameObject obj)
    {
        // Check if this is a prefab instance
        GameObject prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(obj);
        if (prefabAsset == null)
        {
            // It might already be a prefab asset
            if (PrefabUtility.IsPartOfPrefabAsset(obj))
            {
                prefabAsset = obj;
            }
            else
            {
                return false; // Not a prefab
            }
        }

        // Get the asset path and GUID
        string assetPath = AssetDatabase.GetAssetPath(prefabAsset);
        if (string.IsNullOrEmpty(assetPath))
            return false;

        string guid = AssetDatabase.AssetPathToGUID(assetPath);

        // Check if it's in the addressable system
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
            return false;

        return settings.FindAssetEntry(guid) != null;
    }
}
#endif