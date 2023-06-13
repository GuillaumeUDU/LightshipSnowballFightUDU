// Copyright 2022 Niantic, Inc. All Rights Reserved.
﻿using Niantic.ARDK.Extensions.Meshing;
using UnityEngine;

namespace Niantic.ARVoyage.SnowballToss
{
    /// <summary>
    /// Manages the debug functionality for the SnowballToss scene
    /// Visualize Persistent Mesh: turn the visualization of the ARDK mesh on/off
    /// Clear Persistent Mesh: clear the currently recognized ARDK mesh, as well as all snowball splats and collision remnants
    /// </summary>
    public class SnowballTossDebugManager : MonoBehaviour
    {
        // Used to cleanly map features to menu checkbox indices
        private enum CheckboxIndex
        {
            VisualizePersistentMesh = 0,
        }
        
        ARMeshManager arMeshManager;
        SnowballTossManager snowballTossManager;
        
        [SerializeField] DebugMenuGUI debugMenuGUI;


        public bool VisualizePersistentMesh
        {
            get
            {
                return !arMeshManager.UseInvisibleMaterial;
            }

            set
            {
                arMeshManager.UseInvisibleMaterial = !value;
            }
        }

        void Start()
        {
            // SDK
            arMeshManager = FindObjectOfType<ARMeshManager>();
            
            // Scene objects
            snowballTossManager = SceneLookup.Get<SnowballTossManager>();

            // Set initial checkbox values
            debugMenuGUI.SetChecked((int)CheckboxIndex.VisualizePersistentMesh, VisualizePersistentMesh);
        }

        void OnEnable()
        {
            // Subscribe to events
            DebugMenuGUI.EventDebugOption1Checkbox.AddListener(OnEventDebugOption1Checkbox);    // persistent mesh
            DebugMenuGUI.EventDebugOption5Button.AddListener(OnEventDebugOption5Button);        // clear mesh
        }

        void OnDisable()
        {
            // Unsubscribe from events
            DebugMenuGUI.EventDebugOption1Checkbox.RemoveListener(OnEventDebugOption1Checkbox);
            DebugMenuGUI.EventDebugOption5Button.RemoveListener(OnEventDebugOption5Button);
        }


        // persistent mesh
        private void OnEventDebugOption1Checkbox()
        {
            Debug.Log("OnEventDebugOption1Checkbox");
            VisualizePersistentMesh = !VisualizePersistentMesh;
        }

        // clear mesh
        private void OnEventDebugOption5Button()
        {
            Debug.Log("OnEventDebugOption5Button");
            ClearPersistentMesh();
        }

        public void ClearPersistentMesh()
        {
            // Clear the ARDK mesh
            arMeshManager.ClearMeshObjects();

            snowballTossManager.CleanSnowballResidue();
        }
    }
}
