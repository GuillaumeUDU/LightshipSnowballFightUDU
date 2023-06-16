using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ConsoleInteractions : Singleton<ConsoleInteractions>
{
    private Button buttonClicked;
    private EventTrigger triggerClicked;

    //
    private void Start()
    {
        DontDestroyOnLoad(this);
        //EventsSystemHandler.Instance.onTriggerPressTriggerButton += InteractionWithTrigger;
        EventsSystemHandler.Instance.onTriggerReleaseTriggerButton += InteractionWithTriggerRelease;
        EventsSystemHandler.Instance.onTriggerPressSqueezeButton += InteractionWithSqueeze;
        EventsSystemHandler.Instance.onTriggerReleaseSqueezeButton += SqueezeReleased;
        EventsSystemHandler.Instance.onTriggerPressTrackpadButton += InteractionWithTrackpad;
        EventsSystemHandler.Instance.onTriggerReleaseTrackpadButton += TrackpadReleased;
    }

    private void InteractionWithTriggerRelease()
    {
        // find a button??
        // check if correct button??
        // interact if the correct button??

        // loop check to see if a popup or an enemy that is getting interacted with (NEEDED)

        GameObject snowballToss = GameObject.Find("SnowballTossButton");

        if (snowballToss != null)
        {
            InteractionWithPopup(snowballToss);
        }
        else
        {
            //InteractionWithInteractable();
        }
    }

    private void InteractionWithSqueeze()
    {
        GameObject parachuteButtonGO = GameObject.Find("Parachute");
        //Debug.Log("Parachute");

        if (parachuteButtonGO != null)
        {
            //InteractionWithButton(parachuteButtonGO);
            triggerClicked = parachuteButtonGO.transform.GetComponentInChildren<EventTrigger>();
            triggerClicked.triggers[0].callback.Invoke(null);
        }
    }

    private void SqueezeReleased()
    {
        GameObject parachuteButtonGO = GameObject.Find("Parachute");
        //Debug.Log("Parachute");

        if (parachuteButtonGO != null)
        {
            triggerClicked = parachuteButtonGO.transform.GetComponentInChildren<EventTrigger>();
            triggerClicked.triggers[1].callback.Invoke(null); ;
        }
    }

    private void InteractionWithTrackpad(bool firstPress, Vector3 trackpadCoordinates)
    {
        #region Trackpad parachute
        //GameObject parachuteButtonGO = GameObject.Find("Parachute");

        //if (parachuteButtonGO != null)
        //{
        //    //InteractionWithButton(parachuteButtonGO);
        //    triggerClicked = parachuteButtonGO.transform.GetComponentInChildren<EventTrigger>();
        //    triggerClicked.triggers[0].callback.Invoke(null);
        //}
        #endregion

    }

    private void TrackpadReleased()
    {
        #region Trackpad parachute
        //GameObject parachuteButtonGO = GameObject.Find("Parachute");
        //if (parachuteButtonGO != null)
        //{
        //    triggerClicked = parachuteButtonGO.transform.GetComponentInChildren<EventTrigger>();
        //    triggerClicked.triggers[1].callback.Invoke(null); ;
        //}
        #endregion
    }

    // button popup interaction
    private void InteractionWithPopup(GameObject buttonGO)
    {
        buttonClicked = buttonGO.transform.GetComponentInChildren<Button>();
        if (buttonClicked != null)
        {
            buttonClicked.onClick.Invoke();
            //Debug.Log("btn  name: " + buttonGO.transform.name);
        }
    }
    private void InteractionWithButton(GameObject buttonGO)
    {
        triggerClicked = buttonGO.transform.GetComponentInChildren<EventTrigger>();

        foreach (EventTrigger.Entry entry in triggerClicked.triggers)
        {
            // Invoke all the callbacks for the current trigger
            entry.callback.Invoke(null);
        }
        //triggerClicked.triggers[0].callback.Invoke(null);
        //Debug.Log("btn  name: " + buttonGO.transform.name);
    }
}