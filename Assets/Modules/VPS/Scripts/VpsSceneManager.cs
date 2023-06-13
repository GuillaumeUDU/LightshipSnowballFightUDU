using Niantic.ARDK.Extensions;
using Niantic.ARDK.Extensions.Meshing;
using Niantic.ARDK.AR;
using System.Collections.Generic;

using UnityEngine;

namespace Niantic.ARVoyage.Vps
{
    /// <summary>
    /// Constants, GameObjects, game state, and helper methods used by 
    /// various State classes in the VPS scene
    /// Maintains referencs to the user's current VpsDataEntry and associated content that needs to be retained across states
    /// </summary>
    public class VpsSceneManager : MonoBehaviour, ISceneDependency
    {
        public const string FrostFlowerBadgeKey = SaveUtil.KeyBadgeUnlocked + "FrostFlower";
        public const string TouristBadgeKey1 = SaveUtil.KeyBadgeUnlocked + "Tourist1";
        public const string TouristBadgeKey2 = SaveUtil.KeyBadgeUnlocked + "Tourist2";

        [SerializeField] public Camera arCamera;
        [SerializeField] public Camera streetMapCamera;
        [SerializeField] public ARSessionManager arSessionManager;

        [SerializeField] public GameObject returnToHomelandUI;
        [SerializeField] public GameObject returnToHomelandPane;

        // The user's current VpsDataEntry which was selected on the StreetMap
        public VpsDataEntry CurrentVpsDataEntry { get; set; }
        public bool CurrentlyLocalizing { get; set; }

        public VpsBespokeContent VpsBespokeContent { get; set; }

        // Persistent data storage for Bespoke, FrostFlower and Wayspot Anchors
        public PersistentDataDictionary<string, bool> PersistentBespokeStateLookup { get; } = new PersistentDataDictionary<string, bool>(PersistentDataUtility.BespokeStateFilename);
        public PersistentDataDictionary<string, FrostFlower.FrostFlowerSaveData> PersistentFrostFlowerStateLookup { get; } = new PersistentDataDictionary<string, FrostFlower.FrostFlowerSaveData>(PersistentDataUtility.FrostFlowerStateFilename);

        [Header("Meshing")]
        // The VPS experience in ARVoyage uses separate mesh managers with distinct properties per experience
        [SerializeField] public ARMeshManager arMeshMangerBespoke;
        [SerializeField] public ARMeshManager arMeshManagerFrostFlower;

        public bool IsMockARSession
        {
            get
            {
                IARSession session = arSessionManager.ARSession;

                // If there is no session, return true if in editor
                if (session == null)
                {
#if UNITY_EDITOR
                    return true;
#else
                    return false;
#endif
                }

                return arSessionManager.ARSession.RuntimeEnvironment == ARDK.RuntimeEnvironment.Mock;
            }
        }

        private void Awake()
        {
            // Load persistent data.
            PersistentBespokeStateLookup.Load();
            PersistentFrostFlowerStateLookup.Load();

#if !UNITY_EDITOR
            arMeshManagerFrostFlower.SetUseInvisibleMaterial(true);
            arMeshMangerBespoke.SetUseInvisibleMaterial(true);
#endif

            CurrentVpsDataEntry = null;
        }

        public void SetARCameraActive()
        {
            Debug.Log("SetARCameraActive");
            arCamera.gameObject.SetActive(true);
            streetMapCamera.gameObject.SetActive(false);
        }

        public void SetStreetMapCameraActive()
        {
            Debug.Log("SetStreetMapCameraActive");
            arCamera.gameObject.SetActive(false);
            streetMapCamera.gameObject.SetActive(true);
        }

        public bool GetStreetMapCameraActive()
        {
            return streetMapCamera.gameObject.activeSelf;
        }

        public void DisableCameras()
        {
            arCamera.gameObject.SetActive(false);
            streetMapCamera.gameObject.SetActive(false);
        }

        public static bool IsReleaseBuild()
        {
            return !Debug.isDebugBuild || DevSettings.ReleaseBuildDebugMenu;
        }

        /// <summary>
        /// Start the AR session and meshing
        /// </summary>
        public void StartAR(bool meshingCollisionEnabled)
        {
            EnableMeshing(meshingCollisionEnabled);
            arSessionManager.EnableFeatures();
        }

        /// <summary>
        /// Stop the AR session and meshing
        /// </summary>
        public void StopAR()
        {
            DisableMeshing();
            arSessionManager.DisableFeatures();
        }

        private void EnableMeshing(bool collisionEnabled)
        {
            if (collisionEnabled == true)
            {
                arMeshManagerFrostFlower.gameObject.SetActive(true);
            }
            else
            {
                arMeshMangerBespoke.gameObject.SetActive(true);
            }
        }

        private void DisableMeshing()
        {
            // Ensure meshing is disabled when stopping AR.
            arMeshMangerBespoke.gameObject.SetActive(false);
            arMeshManagerFrostFlower.gameObject.SetActive(false);
        }
    }
}
