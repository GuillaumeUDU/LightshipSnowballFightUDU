using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Niantic.ARVoyage
{

    /// <summary>
    /// Utility wrapper for Dictionary to allow for serializing/deserializing to and from 
    /// a file. Used along with PersistantDataUtility below. 
    /// </summary>
    [System.Serializable]
    public class PersistentDataDictionary<K, V> : Dictionary<K, V>, ISerializationCallbackReceiver
    {
        [System.Serializable]
        private class PersistentDataPairList
        {
            public List<PersistentDataPair> persistentDataPairs = new List<PersistentDataPair>();
        }
        [System.Serializable]
        private struct PersistentDataPair
        {
            public K key;
            public V value;
        }
        private string filename;

        [SerializeField] List<PersistentDataPair> persistentDataPairs = new List<PersistentDataPair>();

        public PersistentDataDictionary(string filename)
        {
            this.filename = filename;
        }

        public V GetOrDefault(K key)
        {
            V value = default;
            if (key != null) this.TryGetValue(key, out value);
            return value;
        }

        public void SetAndSave(K key, V value)
        {
            this[key] = value;
            Save();
        }

        public void Save()
        {
            PersistentDataUtility.SerializeObjectToFile(filename, this);
        }

        public void Load()
        {
            PersistentDataUtility.DeserializeObjectFromFile(filename, this);
        }

        // Append contents of an existing JSON file to an existing PersistentDataDictionary.
        public void AppendFromTextAsset(TextAsset textAsset)
        {
            if (textAsset == null) return;

            try
            {
                PersistentDataPairList pairsToAppend = JsonUtility.FromJson<PersistentDataPairList>(textAsset.text);
                foreach (PersistentDataPair pair in pairsToAppend.persistentDataPairs)
                {
                    Debug.Log($"Appending payloads: Key: {pair.key}");
                    this[pair.key] = pair.value;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Data append failed: " + e.ToString());
            }
        }

        // After deserializing, copy pair list to dictionaries.
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            this.Clear();
            foreach (PersistentDataPair pair in persistentDataPairs)
            {
                this.Add(pair.key, pair.value);
            }
        }

        // Before serialization, copy dictionaries to pair list.
        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            persistentDataPairs.Clear();
            foreach (KeyValuePair<K, V> persistentDataPair in this)
            {
                persistentDataPairs.Add(new PersistentDataPair() { key = persistentDataPair.Key, value = persistentDataPair.Value });
            }
        }
    }

    /// <summary>
    /// Utility class for serializing/deserializing arbitrary key/value
    /// pairs to and from persistant JSON files. Used for several types
    /// of user data related to VPS experiences.
    /// </summary>
    public class PersistentDataUtility
    {
        public const string BespokeStateFilename = "bespokeState.json";
        public const string FrostFlowerStateFilename = "frostFlowerState.json";
        public const string WayspotAnchorPayloadFilename = "wayspotAnchorPayloads.json";

        public static void Clear()
        {
            Debug.Log("Deleting persistent data files.");

            // Delete all JSON files.
            DeleteFile(BespokeStateFilename);
            DeleteFile(FrostFlowerStateFilename);
            DeleteFile(WayspotAnchorPayloadFilename);
        }

        public static void DeleteFile(string filename)
        {
            if (System.IO.File.Exists(GetPersistentPath(filename))) System.IO.File.Delete(GetPersistentPath(filename));
        }

        public static string GetPersistentPath(string filename)
        {
            return System.IO.Path.Combine(Application.persistentDataPath, "ARVoyage", filename);
        }

        public static void SerializeObjectToFile(string saveFilename, object saveObject)
        {
            if (saveObject == null || !saveObject.GetType().IsSerializable)
            {
                Debug.LogWarning("Object not serializable");
                return;
            }

            string fullSavePath = GetPersistentPath(saveFilename);

            Debug.Log("Serializing data: " + fullSavePath);
            string saveJson = JsonUtility.ToJson(saveObject);

            (new System.IO.FileInfo(fullSavePath)).Directory.Create();
            System.IO.File.WriteAllText(fullSavePath, saveJson);
        }

        public static void DeserializeObjectFromFile(string restoreFilename, object restoreObject)
        {
            if (restoreObject == null || !restoreObject.GetType().IsSerializable)
            {
                Debug.LogWarning("Object not serializable");
                return;
            }

            string fullRestorePath = GetPersistentPath(restoreFilename);
            if (!System.IO.File.Exists(fullRestorePath))
            {
                Debug.Log("Restore file does not exist: " + fullRestorePath);
                return;
            }

            Debug.Log("Deserializing data: " + fullRestorePath);
            string restoreJson = System.IO.File.ReadAllText(fullRestorePath);

            JsonUtility.FromJsonOverwrite(restoreJson, restoreObject);
        }

        public static void WriteTextToFile(string filename, string text)
        {
            string fullFilePath = GetPersistentPath(filename);

            Debug.Log("Writing text to: " + fullFilePath);

            (new System.IO.FileInfo(fullFilePath)).Directory.Create();
            System.IO.File.WriteAllText(fullFilePath, text);
        }
    }
}
