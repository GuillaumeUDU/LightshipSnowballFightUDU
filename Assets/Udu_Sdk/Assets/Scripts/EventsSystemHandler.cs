using UnityEngine;
using System;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class EventsSystemHandler : Singleton<EventsSystemHandler>
{
    // GENERAL CONSOLE //
    public event Action onEventEnded; // trigger console display plate back to passive/neutral image when event have ended
    public void EventEnded()
    {
        if (onEventEnded != null)
        {
            onEventEnded();
        }
    }

    public event Action onTriggerPressTriggerButton;
    public void TriggerPressTriggerButton()
    {
        if (onTriggerPressTriggerButton != null)
        {
            onTriggerPressTriggerButton();
        }
    }

    public event Action onTriggerPressSqueezeButton;
    public void TriggerPressSqueezeButton()
    {
        if (onTriggerPressSqueezeButton != null)
        {
            onTriggerPressSqueezeButton();
        }
    }

    public event Action <bool, Vector3> onTriggerPressTrackpadButton;
    public void TriggerPressTrackpadButton(bool firstPress, Vector3 trackpadCoordinates)
    {
        if (onTriggerPressTrackpadButton != null)
        {
            onTriggerPressTrackpadButton(firstPress, trackpadCoordinates);
        }
    }


    // COMBAT SCENE //
    public event Action onTriggerCombatVibrations; // when player takes damage
    public void TriggerCombatVibrations()
    {
        if (onTriggerCombatVibrations != null)
        {
            onTriggerCombatVibrations();
        }
    }


    public event Action onTriggerCalibrateForwardHeading;
    public void TriggerCalibrateForwardHeading()
    {
        if (onTriggerCalibrateForwardHeading != null)
        {
            onTriggerCalibrateForwardHeading();
        }
    }


    public event Action<GameObject> onTriggerOnMouseClickButton;
    public void TriggerOnMouseClickButton(GameObject clickedGO)
    {
        if (onTriggerOnMouseClickButton != null)
        {
            onTriggerOnMouseClickButton(clickedGO);
        }
    }

    public event Action onTriggerReleaseTriggerButton;
    internal void TriggerReleaseTriggerButton()
    {
        if (onTriggerReleaseTriggerButton != null)
        {
            onTriggerReleaseTriggerButton();
        }
    }

    public event Action onTriggerReleaseSqueezeButton;
    internal void TriggerReleaseSqueezeButton()
    {
        if (onTriggerReleaseSqueezeButton != null)
        {
            onTriggerReleaseSqueezeButton();
        }
    }

    public event Action onTriggerReleaseTrackpadButton;
    internal void TriggerReleaseTrackpadButton()
    {
        if (onTriggerReleaseTrackpadButton != null)
        {
            onTriggerReleaseTrackpadButton();
        }
    }

    public event Action onTriggerGettingPoints;
    internal void TriggerGettingPoints()
    {
        if (onTriggerGettingPoints != null)
        {
            onTriggerGettingPoints();
        }
    }
}