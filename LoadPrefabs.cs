using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

[Serializable]
public struct AssetData
{
    public string assetReferenceKey;

    // Serializable position
    public float posX;
    public float posY;
    public float posZ;

    // Serializable rotation
    public float rotX;
    public float rotY;
    public float rotZ;
    public float rotW;

    // Serializable scale
    public float scaleX;
    public float scaleY;
    public float scaleZ;

    public string uniqueID;
}

public class LoadPrefabs : MonoBehaviour
{
    [SerializeField] private List<string> availableAssetGuids = new List<string>();
    [SerializeField] private List<AssetData> instantiatedAssets = new List<AssetData>();

    private Dictionary<string, GameObject> loadedInstances = new Dictionary<string, GameObject>();
    //private string savedDataPath => Application.persistentDataPath + "/sceneData_" + gameObject.scene.name + ".abhi";
    private string savedDataPath => Application.dataPath + "/sceneData_" + gameObject.scene.name + ".abhi";

    private void Start()
    {
        LoadAssetsFromFile();
    }
    public void LoadAssets()
    {
#if UNITY_EDITOR
        // Editor loading handled by editor script
#else
        // Runtime loading
        LoadAssetsFromFile();
#endif
    }

    public void SaveAssetsData()
    {
        BinaryFormatter formatter = new BinaryFormatter();
        FileStream file = File.Create(savedDataPath);
        formatter.Serialize(file, instantiatedAssets);
        file.Close();
        Debug.Log($"Saved asset data to {savedDataPath}");
    }

    private void LoadAssetsFromFile()
    {
        if (File.Exists(savedDataPath))
        {
            try
            {
                BinaryFormatter formatter = new BinaryFormatter();
                FileStream file = File.Open(savedDataPath, FileMode.Open);
                instantiatedAssets = (List<AssetData>)formatter.Deserialize(file);
                file.Close();

                Debug.Log($"Loaded {instantiatedAssets.Count} assets from file");
                foreach (AssetData data in instantiatedAssets)
                {
                    InstantiateAsset(data);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error loading assets: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning($"No saved data file found at {savedDataPath}");
        }
    }

    private void InstantiateAsset(AssetData data)
    {
        // Create AssetReference from the stored GUID
        AssetReference assetRef = new AssetReference(data.assetReferenceKey);

        // Create position, rotation and scale from individual components
        Vector3 position = new Vector3(data.posX, data.posY, data.posZ);
        Quaternion rotation = new Quaternion(data.rotX, data.rotY, data.rotZ, data.rotW);
        Vector3 scale = new Vector3(data.scaleX, data.scaleY, data.scaleZ);

        // Instantiate the asset with the serialized transform data
        assetRef.InstantiateAsync(position, rotation).Completed += (AsyncOperationHandle<GameObject> obj) =>
        {
            if (obj.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
            {
                GameObject instance = obj.Result;
                instance.transform.localScale = scale;
                loadedInstances.Add(data.uniqueID, instance);
            }
        };
    }
}