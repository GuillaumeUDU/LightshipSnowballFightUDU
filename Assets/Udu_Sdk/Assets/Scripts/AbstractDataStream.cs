using UnityEngine;

public abstract class AbstractDataStream : MonoBehaviour
{
    #region Fields : IMU
    protected long _timestamp;
    protected Quaternion _orientation = new Quaternion();
    protected Vector3 _acceleration = new Vector3();
    protected Vector3 _angularVelocity = new Vector3();
    protected float _magneticHeading;
    protected Vector3 _trackpadCoordinates = new Vector3();
    #endregion

    protected bool firstTrackpadPress = true;
    protected bool trackpadPressed = false;
    protected bool trackpadReleased = false;

    #region Fields : Buttons
    protected bool squeezePressed = false;
    protected bool triggerPressed = false;

    protected bool triggerReleased = false;
    protected bool squeezeReleased = false;

    protected bool firstSqueezePress = true;
    protected bool firstTriggerPress = true;
    #endregion

    #region Fields : Gesture recognition
    protected byte gestureScore01, gestureScore02, gestureScore03, gestureScore04;
    protected bool hasGestreReturned = false;
    #endregion

    #region Unity Methods
    protected virtual void Awake()
    {
        DontDestroyOnLoad(this.gameObject);
    }
    #endregion

    #region Protected Methods : Set console data
    protected abstract void SetTimestamp();
    protected abstract void SetAcceleration();
    protected abstract void SetAngularVelocity();
    protected abstract void SetOrientation();
    protected abstract void SetButtonPressed();
    protected abstract void SetTrackpadCoordinates();
    protected abstract void SetMagneticHeading();
    protected abstract void SetEdgeImpulseData();
    #endregion

    #region Public Methods : Get console data
    public abstract long GetTimestamp();
    public abstract Vector3 GetAcceleration();
    public abstract Vector3 GetAngularVelocity();
    public abstract Quaternion GetOrientation();
    public abstract Vector3 GetTrackpadCoordinates();
    public abstract float GetMagneticHeading();

    #region Console Methods : Gesture Recognition
    public abstract byte GetGesture01();
    public abstract byte GetGesture02();
    public abstract byte GetGesture03();
    public abstract byte GetGesture04();
    #endregion
    #endregion

    #region Public Methods : Console functionality
    #region Console Methods : Gesture recognition
    public abstract void StartGestureRecognition();
    public abstract void StopGestureRecognition();
    public abstract bool HasReturnedGesture();
    #endregion

    #region Console Methods Outputs : Haptics
    public abstract void SetVibrationAndStart(string filename, bool looping);
    public abstract void StartVibration();
    public abstract void SetVibration(string filename);
    #endregion

    #region Console Methods Outputs : LEDs
    public abstract void SetLED(bool isFlashing, int r, int g, int b, int brightness, int durationInSeconds);
    public abstract void SetLEDConstantColor(Color color, int brightness);
    public abstract void SetLEDFlashingColor(Color color, int brightness, short flashingInterval, int durationInSeconds);
    public abstract void SetLEDOff();
    #endregion

    #region Console Methods Outputs : Display 
    public abstract void SetDisplayFile(string filename);
    #endregion

    #region Console Methods Ouputs : Multi
    public abstract void StartVibrationAndLEDs(string filename, Color color);
    public abstract void SetImageVibrationAndLED(string imageName, string vibrationName, Color color);
    public abstract void SetImageAndLEDs(string imageName, Color color);
    public abstract void StartVibrationAndSetImage(string vibrationName, string imageName);
    #endregion

    #region Console Methods : Buttons
    protected virtual void TriggerButtonPressed()
    {
        EventsSystemHandler.Instance.TriggerPressTriggerButton();
    }

    protected virtual void TriggerButtonReleased()
    {
        EventsSystemHandler.Instance.TriggerReleaseTriggerButton();
    }

    protected virtual void SqueezeButtonPressed()
    {
        EventsSystemHandler.Instance.TriggerPressSqueezeButton();
    }

    protected virtual void SqueezeButtonReleased()
    {
        EventsSystemHandler.Instance.TriggerReleaseSqueezeButton();
    }
    #endregion

    #region Console Methods : Trackpad
    protected virtual void TrackpadButtonPressed(bool firstPress, Vector3 trackpadCoordinates)
    {
        EventsSystemHandler.Instance.TriggerPressTrackpadButton(firstPress, trackpadCoordinates);
    }

    protected virtual void TrackpadButtonReleased()
    {
        EventsSystemHandler.Instance.TriggerReleaseTrackpadButton();
    }
    #endregion
    #endregion
}