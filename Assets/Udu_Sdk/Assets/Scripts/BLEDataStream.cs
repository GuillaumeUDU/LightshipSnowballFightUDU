using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BLEDataStream : AbstractDataStream
{
    #region Field variables
    #region Private fields
    private static List<byte> motionStream = new List<byte>();
    private static List<byte> buttonStream = new List<byte>();
    private static List<byte> trackpadStream = new List<byte>();
    private static List<byte> edgeImpulseStream = new List<byte>();

    private BLEUDUManager bleManager;

    private byte prevGestureScore01, prevGestureScore02, prevGestureScore03, prevGestureScore04;
    private float prevTrackpadX, prevTrackpadY, prevTrackpadZ;
    #endregion

    #region Public fields
    [Header("Raw Byte Text Debug?")]
    public bool rawByteDebug = false;
    public Text rawbyte_text;

    [Header("Trackpad values")]
    public Text xValueText;
    public Text yValueText;
    public Text zValueText;
    #endregion
    #endregion

    #region Start function
    void Start()
    {
        bleManager = GetComponent<BLEUDUManager>();
    }
    #endregion

    #region Setting stream bytes
    public void SetIMUBytes(byte[] arr)
    {
        List<byte> s = new List<byte>();
        foreach (byte b in arr)
        {
            s.Add(b);
        }
        motionStream = s;
        if (motionStream.Count >= 26)
        {
            SetIMUData();
        }
    }

    public void SetButtonBytes(byte[] arr)
    {
        List<byte> s = new List<byte>();
        foreach (byte b in arr)
        {
            s.Add(b);
        }
        buttonStream = s;
        SetButtonPressed();
    }

    public void SetTrackpadBytes(byte[] arr)
    {
        List<byte> s = new List<byte>();
        foreach (byte b in arr)
        {
            s.Add(b);
        }
        trackpadStream = s;
        SetTrackpadCoordinates();
    }

    public void SetEdgeImpluseBytes(byte[] arr)
    {
        List<byte> s = new List<byte>();
        foreach (byte b in arr)
        {
            s.Add(b);
        }
        edgeImpulseStream = s;
        if (edgeImpulseStream.Count >= 4)
        {
            SetEdgeImpulseData();
        }
    }
    #endregion

    #region Setting console data
    private void SetIMUData()
    {
        SetTimestamp();
        SetAcceleration();
        SetAngularVelocity();
        SetOrientation();
        SetMagneticHeading();
    }

    protected override void SetTimestamp()
    {
        byte[] timestampBytes = new byte[8] { 0x00, 0x00, 0x00, 0x00, motionStream[0], motionStream[1], motionStream[2], motionStream[3] };
        _timestamp = System.BitConverter.ToInt64(timestampBytes, 0);
    }

    protected override void SetAcceleration()
    {
        byte[] axBytes = new byte[2] { motionStream[4], motionStream[5] };
        byte[] ayBytes = new byte[2] { motionStream[6], motionStream[7] };
        byte[] azBytes = new byte[2] { motionStream[8], motionStream[9] };

        _acceleration.x = System.BitConverter.ToInt16(axBytes, 0);
        _acceleration.y = System.BitConverter.ToInt16(ayBytes, 0);
        _acceleration.z = System.BitConverter.ToInt16(azBytes, 0);
    }

    protected override void SetAngularVelocity()
    {
        byte[] avxBytes = new byte[2] { motionStream[10], motionStream[11] };
        byte[] avyBytes = new byte[2] { motionStream[12], motionStream[13] };
        byte[] avzBytes = new byte[2] { motionStream[14], motionStream[15] };

        _angularVelocity.x = System.BitConverter.ToInt16(avxBytes, 0);
        _angularVelocity.y = System.BitConverter.ToInt16(avyBytes, 0);
        _angularVelocity.z = System.BitConverter.ToInt16(avzBytes, 0);
    }

    protected override void SetOrientation()
    {
        byte[] qxBytes = new byte[2] { motionStream[16], motionStream[17] };
        byte[] qyBytes = new byte[2] { motionStream[18], motionStream[19] };
        byte[] qzBytes = new byte[2] { motionStream[20], motionStream[21] };
        byte[] qwBytes = new byte[2] { motionStream[22], motionStream[23] };

        _orientation.x = System.BitConverter.ToInt16(qxBytes, 0);
        _orientation.y = System.BitConverter.ToInt16(qyBytes, 0);
        _orientation.z = System.BitConverter.ToInt16(qzBytes, 0);
        _orientation.w = System.BitConverter.ToInt16(qwBytes, 0);
    }

    protected override void SetMagneticHeading()
    {
        byte[] magneticHeading = new byte[2] { motionStream[24], motionStream[25] };

        _magneticHeading = System.BitConverter.ToInt16(magneticHeading, 0);
    }

    protected override void SetButtonPressed()
    {
        if (buttonStream.Count > 0)
        {
            int buttonpress = (int)buttonStream[0];
            squeezePressed = buttonpress == 1 || buttonpress == 3;
            triggerPressed = buttonpress == 2 || buttonpress == 3;
        }

        if (!triggerPressed)
        {
            firstTriggerPress = true;
        }
        if (!squeezePressed)
        {
            firstSqueezePress = true;
        }

        if (squeezePressed && firstSqueezePress)
        {
            SqueezeButtonPressed();
            Debug.Log("Squeeze");
            firstSqueezePress = false;
            squeezeReleased = true;
        }
        if (triggerPressed && firstTriggerPress)
        {
            TriggerButtonPressed();
            Debug.Log("Trigger");
            firstTriggerPress = false;
            triggerReleased = true;
        }
        if (!triggerPressed && triggerReleased)
        {
            TriggerButtonReleased();
            Debug.Log("Trigger released");
            triggerReleased = false;
        }
        if (!squeezePressed && squeezeReleased)
        {
            SqueezeButtonReleased();
            Debug.Log("Squeeze released");
            squeezeReleased = false;
        }
    }

    protected override void SetTrackpadCoordinates()
    {
        byte[] trackpadxCoordinates = new byte[2] { trackpadStream[0], trackpadStream[1] };
        byte[] trackpadyCoordinates = new byte[2] { trackpadStream[2], trackpadStream[3] };
        byte[] trackpadzCoordinates = new byte[2] { trackpadStream[4], trackpadStream[5] };

        _trackpadCoordinates.x = System.BitConverter.ToInt16(trackpadxCoordinates, 0);
        _trackpadCoordinates.y = System.BitConverter.ToInt16(trackpadyCoordinates, 0);
        _trackpadCoordinates.z = System.BitConverter.ToInt16(trackpadzCoordinates, 0);

        if (
           _trackpadCoordinates.x != prevTrackpadX
           || _trackpadCoordinates.y != prevTrackpadY
           || _trackpadCoordinates.z != prevTrackpadZ
           )
        {
            prevTrackpadX = _trackpadCoordinates.x;
            prevTrackpadY = _trackpadCoordinates.y;
            prevTrackpadZ = _trackpadCoordinates.z;
        }

        if (prevTrackpadX == _trackpadCoordinates.x || prevTrackpadY == _trackpadCoordinates.y || prevTrackpadZ == _trackpadCoordinates.z)
            StartCoroutine(CheckTradpadIdling(2f));
    }

    private IEnumerator CheckTradpadIdling(float duration)
    {
        float timer = duration;

        while (timer > 0)
        {
            yield return new WaitForSeconds(1f);

            timer--;
        }

        _trackpadCoordinates.x = 0f;
        _trackpadCoordinates.y = 0f;
        _trackpadCoordinates.z = 0f;
    }


    protected override void SetEdgeImpulseData()
    {
        byte[] _gestureScore01 = new byte[1] { edgeImpulseStream[0] };
        byte[] _gestureScore02 = new byte[1] { edgeImpulseStream[1] };
        byte[] _gestureScore03 = new byte[1] { edgeImpulseStream[2] };
        byte[] _gestureScore04 = new byte[1] { edgeImpulseStream[3] };

        gestureScore01 = _gestureScore01[0]; // slam 
        gestureScore02 = _gestureScore02[0]; // slash TLBR
        gestureScore03 = _gestureScore03[0]; // slash TRBL
        gestureScore04 = _gestureScore04[0]; // stab
    }
    #endregion

    #region Getting console data
    public override long GetTimestamp()
    {
        return _timestamp;
    }

    public override Vector3 GetAcceleration()
    {
        return _acceleration;
    }

    public override Vector3 GetAngularVelocity()
    {
        return _angularVelocity;
    }

    public override Quaternion GetOrientation()
    {
        return _orientation;
    }

    public override Vector3 GetTrackpadCoordinates()
    {
        return _trackpadCoordinates;
    }

    public override float GetMagneticHeading()
    {
        return _magneticHeading;
    }

    public override byte GetGesture01() // slam 
    {
        return gestureScore01;
    }

    public override byte GetGesture02() //slash TLBR
    {
        return gestureScore02;
    }

    public override byte GetGesture03() // slash TRBL
    {
        return gestureScore03;
    }

    public override byte GetGesture04() // stab
    {
        return gestureScore04;
    }
    #endregion

    #region Gesture recognition
    public override void StartGestureRecognition()
    {
        bleManager?.StartGestureRecognition();
    }

    public override void StopGestureRecognition()
    {
        bleManager?.StopGestureRecognition();
    }

    public override bool HasReturnedGesture()
    {
        if (
            gestureScore01 != prevGestureScore01
            || gestureScore02 != prevGestureScore02
            || gestureScore03 != prevGestureScore03
            || gestureScore04 != prevGestureScore04
            )
        {
            prevGestureScore01 = gestureScore01;
            prevGestureScore02 = gestureScore02;
            prevGestureScore03 = gestureScore03;
            prevGestureScore04 = gestureScore04;

            hasGestreReturned = true;
        }
        else
        {
            gestureScore01 = 0;
            gestureScore02 = 0;
            gestureScore03 = 0;
            gestureScore04 = 0;
            hasGestreReturned = false;
        }
        return hasGestreReturned;
    }
    #endregion

    #region Public Methods : Haptics
    public override void SetVibrationAndStart(string filename, bool looping)
    {
        bleManager?.SetVibrationAndStart(filename);
    }

    public override void StartVibration()
    {
        bleManager?.StartVibration();
    }

    public override void SetVibration(string filename)
    {
        bleManager?.SetVibration(filename);
    }
    #endregion

    #region Public Methods : Display
    public override void SetDisplayFile(string filename)
    {
        bleManager?.SetImage(filename);
    }
    #endregion

    #region Public Methods : LEDs
    public override void SetLED(bool isFlashing, int r, int g, int b, int brightness, int durationInSeconds)
    {
        bleManager?.SetLED(isFlashing, r, g, b, brightness, durationInSeconds);
    }

    public override void SetLEDOff()
    {
        bleManager?.SetLEDOff();
    }

    public override void SetLEDFlashingColor(Color color, int brightness, short flashingInterval, int durationInSeconds)
    {
        bleManager?.SetLEDFlashingColor(color, brightness, flashingInterval, durationInSeconds);
    }

    public override void SetLEDConstantColor(Color color, int brightness)
    {
        bleManager?.SetLEDConstantColor(color, brightness);
    }
    #endregion

    #region Public Methods : Multi Methods
    public override void StartVibrationAndLEDs(string vibrationName, Color color)
    {
        bleManager?.StartVibrationAndLEDs(vibrationName, color);
    }

    public override void SetImageVibrationAndLED(string imageName, string vibrationName, Color color)
    {
        bleManager?.SetImageVibrationAndLEDs(imageName, vibrationName, color);
    }

    public override void SetImageAndLEDs(string imageName, Color color)
    {
        bleManager?.SetImageAndLEDs(imageName, color);
    }

    public override void StartVibrationAndSetImage(string vibrationName, string imageName)
    {
        bleManager?.StartVibrationAndSetImage(imageName, imageName);
    }
    #endregion

    #region console Methods : Helpers
    private void OnApplicationQuit()
    {
        bleManager?.SetState(BLEUDUManager.States.Unsubscribe, 5f);
    }
    #endregion
}