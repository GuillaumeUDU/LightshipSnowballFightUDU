using UDU;
using UnityEngine;
using UnityEngine.UI;

public class ControllerInteractions : UDU.Singleton<ControllerInteractions>
{
    string gifName;
    string wavName;
    Color ledColor;

    void Start()
    {
        EventsSystemHandler.Instance.onControllerConnect += InitialControllerConnection;
        EventsSystemHandler.Instance.onTriggerReleaseTriggerButton += InteractionWithTriggerRelease;
        EventsSystemHandler.Instance.onTriggerPressTriggerButton += InteractioWithTriggerButtonPressed;
    }

    private void InitialControllerConnection()
    {
        UDUOutputs.SetImageVibrationAndLEDs("snowballdisplay.gif", "", Color.white);
    }

    public void InteractionWithTriggerRelease()
    {
        // find a button??
        // check if correct button??
        // interact if the correct button??

        GameObject snowballToss = GameObject.Find("SnowballTossButton");

        if (snowballToss != null)
        {
            InteractionWithPopup(snowballToss);
            UDUOutputs.SetVibrationAndStart("swoosh.wav");
        }
    }

    private void InteractioWithTriggerButtonPressed()
    {
        GameObject startButton = GameObject.Find("StartButton"); // instructions
        GameObject okButton = GameObject.Find("OkButton"); // Ar warning
        GameObject restartButton = GameObject.Find("RestartButton"); // game over

        if (startButton != null)
        {
            InteractionWithPopup(startButton);
        }
        else if (okButton != null)
        {
            InteractionWithPopup(okButton);
        }
        else if (restartButton != null)
        {
            InteractionWithPopup(restartButton);
        }
    }

    // button popup interaction
    private void InteractionWithPopup(GameObject buttonGO)
    {
        Button buttonClicked = buttonGO.transform.GetComponentInChildren<Button>();
        if (buttonClicked != null)
        {
            buttonClicked.onClick.Invoke();
            //Debug.Log("btn  name: " + buttonGO.transform.name);
        }
    }

    public void SetOutputsAndInvokeToAnew(string _gif, string _wav, Color _led)
    {
        gifName = _gif;
        wavName = _wav;
        ledColor = _led;
        Invoke("SetOutputs", 1f);
    }

    private void SetOutputs()
    {
        UDUOutputs.SetImageVibrationAndLEDs(gifName, wavName, ledColor);
    }
}