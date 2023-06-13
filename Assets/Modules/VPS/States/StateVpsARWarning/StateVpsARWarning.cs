// Copyright 2022 Niantic, Inc. All Rights Reserved.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Niantic.ARDK.AR.ARSessionEventArgs;
using Niantic.ARDK.AR;

namespace Niantic.ARVoyage.Vps
{
    /// <summary>
    /// Inherits from universal state used by all scenes, displaying a GUI with warning text about using AR.
    /// Overrides here turn on AR, needed the particular UX flow in VPS.
    /// Its next state (value assigned via inspector) is StateVpsLocalization.
    /// </summary>
    public class StateVpsARWarning : StateWarning
    {
        private VpsSceneManager vpsSceneManager;
        private IARSession arSession;

        protected override void Awake()
        {
            vpsSceneManager = SceneLookup.Get<VpsSceneManager>();
            fader = SceneLookup.Get<Fader>();

            // By default, we're not the first state; start off disabled
            gameObject.SetActive(false);
        }

        protected override void OnEnable()
        {
            // wait to fade in, until the device camera is active
            fadeInDuringEnable = false;

            base.OnEnable();

            // Meshing collision is only enabled for non-bespoke locations. 
            bool meshingCollisionEnabled = !vpsSceneManager.CurrentVpsDataEntry.bespokeEnabled;

            // activate AR camera
            // start AR
            arSession = null;
            ARSessionFactory.SessionInitialized += OnARSessionInitialized;
            vpsSceneManager.SetARCameraActive();
            vpsSceneManager.StartAR(meshingCollisionEnabled);

            // Disable the debug button during warning state
            SceneLookup.Get<DebugMenuButton>().gameObject.SetActive(false);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ARSessionFactory.SessionInitialized -= OnARSessionInitialized;
            if (arSession != null) arSession.FrameUpdated -= OnFrameUpdated;
        }

        private void OnARSessionInitialized(AnyARSessionInitializedArgs args)
        {
            arSession = args.Session;
            arSession.FrameUpdated += OnFrameUpdated;
        }

        private void OnFrameUpdated(FrameUpdatedArgs args)
        {
            Debug.Log("StateVpsARWarning OnFrameUpdated");
            arSession.FrameUpdated -= OnFrameUpdated;

            // Now that device's camera is active, fade in
            fader.FadeSceneIn(Color.white, 0.5f);
            StartCoroutine(DemoUtil.FadeInGUI(gui, fader, initialDelay: 0.75f));
        }
    }
}
