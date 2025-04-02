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

    // Helper properties to convert between Unity types and serializable fields
    [NonSerialized]
    private Vector3 _position;

    [NonSerialized]
    private Quaternion _rotation;

    [NonSerialized]
    private Vector3 _scale;

    public Vector3 position
    {
        get { return new Vector3(posX, posY, posZ); }
        set { posX = value.x; posY = value.y; posZ = value.z; }
    }

    public Quaternion rotation
    {
        get { return new Quaternion(rotX, rotY, rotZ, rotW); }
        set { rotX = value.x; rotY = value.y; rotZ = value.z; rotW = value.w; }
    }

    public Vector3 scale
    {
        get { return new Vector3(scaleX, scaleY, scaleZ); }
        set { scaleX = value.x; scaleY = value.y; scaleZ = value.z; }
    }
}

public class LoadPrefabs : MonoBehaviour
{
    [SerializeField] private List<string> availableAssetGuids = new List<string>();
    [SerializeField] private List<AssetData> instantiatedAssets = new List<AssetData>();

    public Dictionary<string, GameObject> loadedInstances = new Dictionary<string, GameObject>();
    private string savedDataPath => Application.persistentDataPath + "/sceneData_" + gameObject.scene.name + ".dat";
    private void Start()
    {
        LoadAssets();
    }
    public void LoadAssets()
    {
//#if UNITY_EDITOR
        // Editor loading handled by editor script
//#else
        // Runtime loading
        LoadAssetsFromFile();
//#endif
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
                Debug.Log($"Successfully instantiated asset: {instance.name}");
            }
            else
            {
                Debug.LogError($"Failed to instantiate asset: {obj.OperationException}");
            }
        };
    }
}