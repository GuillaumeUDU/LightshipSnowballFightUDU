// Copyright 2022 Niantic, Inc. All Rights Reserved.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Niantic.ARVoyage
{
    /// <summary>
    /// Add this interface to any Monobehaviour to have it be auto-added to the lookup while the scene loads.
    /// If this is the first loaded scene, they will be added before Awake. Otherwise, they will be added on first access of the lookup.
    /// </summary>
    public interface ISceneDependency { }

    /// <summary>
    /// Add this interface to any Monobehaviour to have it be auto-added to the lookup while the scene loads.
    /// If this is the first loaded scene, they will be added before Awake. Otherwise, they will be added on first access of the lookup.
    /// The entry will be flagged to persist across scenes AND will be set to DontDestroyOnLoad
    /// </summary>
    public interface IPersistentSceneDependency { }

    /// <summary>
    /// Type-based lookup of scene objects
    /// Monobehaviors that implement the ISceneDependency inteface will automatically be added to the lookup
    /// after the scene is loaded, before Awake methods are called
    /// Obects can also add themselves at runtime
    /// Cleared when scenes are unloaded
    /// </summary>
    public static class SceneLookup
    {
        private struct SceneLookupEntry
        {
            public readonly bool PersistAcrossScenes;
            public readonly object Value;

            public SceneLookupEntry(object value, bool persistAcrossScenes)
            {
                this.Value = value;
                this.PersistAcrossScenes = persistAcrossScenes;
            }
        }

        private static Dictionary<Type, SceneLookupEntry> lookup = new Dictionary<Type, SceneLookupEntry>();
        private static bool ranAutoInitializationForScene;

        /// <summary>
        /// Get an object from the lookup by type
        /// </summary>
        public static T Get<T>(bool warnIfNotFound = true) where T : class
        {
            AddSceneDependenciesIfNecessary();

            if (lookup.TryGetValue(typeof(T), out SceneLookupEntry entry))
            {
                return entry.Value as T;
            }
            else
            {
                if (warnIfNotFound)
                {
                    Debug.LogWarning(typeof(SceneLookup).Name + " didn't find object of type " + typeof(T).Name);
                }

                return null;
            }
        }

        /// <summary>
        /// Try to get an object from the lookup by type
        /// </summary>
        public static bool TryGet<T>(out T obj) where T : class
        {
            AddSceneDependenciesIfNecessary();

            if (lookup.TryGetValue(typeof(T), out SceneLookupEntry entry))
            {
                obj = entry.Value as T;
                return true;
            }
            else
            {
                obj = null;
                return false;
            }
        }

        /// <summary>
        /// Add an object to the lookup
        /// </summary>
        /// <param name="obj">The object</param>
        /// <param name="persistAcrossScenes">Should this object persist in the lookup across scenes</param>
        public static void Add<T>(T obj, bool persistAcrossScenes)
        {
            AddInternal(obj.GetType(), obj, persistAcrossScenes);
        }

        /// <summary>
        /// Remove an object from the lookup
        /// </summary>
        /// <param name="obj">The object</param>
        public static void Remove<T>(T obj)
        {
            Type type = obj.GetType();
            if (lookup.Remove(type))
            {
                Debug.Log($"{nameof(SceneLookup)} removed from lookup: {obj} of type {type}");
            }
            else
            {
                Debug.LogWarning($"{nameof(SceneLookup)} couldn't find entry to remove from lookup: {obj} of type {type}");
            }
        }

        static SceneLookup()
        {
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            // When unloading the scene, clear out any non-persistent entries
            List<Type> typeKeysToRemove = new List<Type>();

            foreach (var entry in lookup)
            {
                if (!entry.Value.PersistAcrossScenes)
                {
                    typeKeysToRemove.Add(entry.Key);
                }
            }

            foreach (Type key in typeKeysToRemove)
            {
                lookup.Remove(key);
            }

            ranAutoInitializationForScene = false;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
        {
            // In case nothing has run the add scene dependency logic by the time the scene has loaded, run it
            AddSceneDependenciesIfNecessary();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AddSceneDependenciesIfNecessary()
        {
            // Bail if already added dependencies for this scene
            if (ranAutoInitializationForScene)
            {
                return;
            }

            MonoBehaviour[] monoBehaviours = GameObject.FindObjectsOfType<MonoBehaviour>(includeInactive: true);
            foreach (MonoBehaviour monoBehaviour in monoBehaviours)
            {
                if (monoBehaviour is IPersistentSceneDependency)
                {
                    Type type = monoBehaviour.GetType();
                    // Don't replace instances of IPersistentSceneDependency since they are designed to persist across scenes
                    if (lookup.ContainsKey(type))
                    {
                        Debug.Log($"{nameof(SceneLookup)} ingoring addition of a second {nameof(IPersistentSceneDependency)} for type {type}.");
                    }
                    else
                    {
                        // Set the Monobehaviour to DontDestroyOnLoad so it persists throughout the application
                        UnityEngine.Object.DontDestroyOnLoad(monoBehaviour);
                        AddInternal(type, monoBehaviour, persistAcrossScenes: true);
                    }
                }
                else if (monoBehaviour is ISceneDependency)
                {
                    AddInternal(monoBehaviour.GetType(), monoBehaviour, persistAcrossScenes: false);
                }
            }
            ranAutoInitializationForScene = true;
        }

        private static void AddInternal(Type type, object obj, bool persistAcrossScenes)
        {
            Debug.Log($"{nameof(SceneLookup)} Add to lookup {type} -> {obj}. [{nameof(persistAcrossScenes)} {persistAcrossScenes}]");

            if (lookup.ContainsKey(type))
            {
                Debug.LogWarning("Updating existing lookup for type " + type.Name +
                    " from " + lookup[type] + " -> " + obj);
            }

            lookup[type] = new SceneLookupEntry(obj, persistAcrossScenes: persistAcrossScenes);
        }
    }
}
