using System;
using System.Collections;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

public class BLEUDUManager : MonoBehaviour
{
    #region Field variables
    public static string[] DeviceNames = { "UDU Console BLE" };

    private bool _connected = false;
    private float _timeout = 0f;
    private States _state = States.None;
    private string _deviceAddress;
    private bool _rssiOnly = false;
    private int _rssi = 0;

    private Text textLog;

    private string currentVibrationFilename;
    private BLEDataStream bleDataStream;

    public bool Connected { get => _connected; set => _connected = value; }
    #endregion

    #region Start & Update functions
    void Start()
    {
        InitialConsoleConnection();
    }

    void Update()
    {
        ConsoleConnection();
    }
    #endregion

    #region Console connection
    private void InitialConsoleConnection()
    {
        if (!Application.isEditor)
        {
            Reset();
            bleDataStream = GetComponent<BLEDataStream>();
            BluetoothLEHardwareInterface.Initialize(true, true, () =>
            {
                StatusMessage = "Setting up connection with BLE device...";
                SetState(States.Scan, 0.5f);

            }, (error) =>
            {
                StatusMessage = "Error during initialize: " + error;
            });
        }
    }

    private void ConsoleConnection()
    {
        if (_timeout > 0f)
        {
            _timeout -= Time.deltaTime;
            if (_timeout <= 0f)
            {
                _timeout = 0f;

                switch (_state)
                {
                    case States.None:
                        Thread.Sleep(200);
                        break;
                    case States.Scan:
                        StatusMessage = "Scanning Peripherals...";
                        BluetoothLEHardwareInterface.ScanForPeripheralsWithServices(null, (address, name) =>
                        {
                            // if your device does not advertise the rssi and manufacturer specific data
                            // then you must use this callback because the next callback only gets called
                            // if you have manufacturer specific data
                            if (!_rssiOnly)
                            {
                                //StatusMessage = "Found device " + name;
                                if (Array.FindAll(DeviceNames, s => s.Equals(name)).Length > 0)
                                {
                                    StatusMessage = "Found " + name;

                                    BluetoothLEHardwareInterface.StopScan();

                                    // found a device with the name we want
                                    // this example does not deal with finding more than one
                                    _deviceAddress = address;
                                    SetState(States.Connect, 0.5f);
                                }
                            }
                        }, (address, name, rssi, bytes) =>
                        {
                            // use this one if the device responses with manufacturer specific data and the rssi
                            if (Array.FindAll(DeviceNames, s => s.Equals(name)).Length > 0)
                            {
                                StatusMessage = "Found " + name;

                                if (_rssiOnly)
                                {
                                    _rssi = rssi;
                                }
                                else
                                {
                                    BluetoothLEHardwareInterface.StopScan();

                                    // found a device with the name we want
                                    // this example does not deal with finding more than one
                                    _deviceAddress = address;
                                    SetState(States.Connect, 0.5f);
                                }
                            }

                        }, _rssiOnly); // this last setting allows RFduino to send RSSI without having manufacturer data

                        if (_rssiOnly)
                            SetState(States.ScanRSSI, 0.5f);
                        break;

                    case States.ScanRSSI:
                        break;

                    case States.Connect:
                        StatusMessage = "Connecting...";
                        BluetoothLEHardwareInterface.ConnectToPeripheral(_deviceAddress, null, null, (address, serviceUUID, characteristicUUID) =>
                        {
                            SetState(States.RequestMTU, 2f);
                        });
                        break;
                    case States.RequestMTU:
                        StatusMessage = "Requesting MTU";
                        BluetoothLEHardwareInterface.RequestMtu(_deviceAddress, 251, (address, newMTU) =>
                        {
                            StatusMessage = "MTU set to " + newMTU.ToString();
                            SetState(States.Subscribe, 0.1f);
                        });
                        break;
                    case States.Subscribe:
                        StatusMessage = "Subscribing to characteristics...";
                        InitializeUDUConsole();
                        break;
                    case States.Unsubscribe:
                        BluetoothLEHardwareInterface.UnSubscribeCharacteristic(_deviceAddress, UduGattUuid.ButtonsServiceUUID, UduGattUuid.ButtonEventCharacteristicUUID, (callbackText) =>
                        {
                            StatusMessage = callbackText;

                            BluetoothLEHardwareInterface.UnSubscribeCharacteristic(_deviceAddress, UduGattUuid.IMUServiceUUID, UduGattUuid.IMUDataCharacteristicUUID, null);
                            BluetoothLEHardwareInterface.UnSubscribeCharacteristic(_deviceAddress, UduGattUuid.GestureRecognitionServiceUUID, UduGattUuid.GestureRecognitionCharacteristicUUID, null);
                            BluetoothLEHardwareInterface.UnSubscribeCharacteristic(_deviceAddress, UduGattUuid.TrackpadService, UduGattUuid.TrackpadCharacteristicUUID, null);
                        });

                        SetState(States.Disconnect, 4f);
                        break;
                    case States.Disconnect:
                        StatusMessage = "Commanded disconnect.";

                        if (Connected)
                        {
                            BluetoothLEHardwareInterface.DisconnectPeripheral(_deviceAddress, (address) =>
                            {
                                StatusMessage = "Device disconnected";
                                BluetoothLEHardwareInterface.DeInitialize(() =>
                                {
                                    Connected = false;
                                    _state = States.None;
                                });
                            });
                        }
                        else
                        {
                            BluetoothLEHardwareInterface.DeInitialize(() =>
                            {
                                _state = States.None;
                            });
                        }
                        break;
                }
            }
        }
    }
    #endregion

    #region BLE Connection: Notify characteristic subscription
    private void SubscribeToButtonCharacteristic()
    {
        BluetoothLEHardwareInterface.SubscribeCharacteristicWithDeviceAddress(_deviceAddress, UduGattUuid.ButtonsServiceUUID, UduGattUuid.ButtonEventCharacteristicUUID, (notifyAddress, notifyCharacteristic) =>
        {
            _state = States.None;
            StatusMessage = "Subscribed to Button characteristic";
        }, (address, characteristicUUID, bytes) =>
        {
            if (_state != States.None)
            {
                // some devices do not properly send the notification state change which calls
                // the lambda just above this one so in those cases we don't have a great way to
                // set the state other than waiting until we actually got some data back.
                // The esp32 sends the notification above, but if yuor device doesn't you would have
                // to send data like pressing the button on the esp32 as the sketch for this demo
                // would then send data to trigger this.
                StatusMessage = "Waiting for user action (2)...";

                _state = States.None;
            }
            bleDataStream.SetButtonBytes(bytes);
        });
    }

    private void SubscribeToTrackpadCharacteristic()
    {
        BluetoothLEHardwareInterface.SubscribeCharacteristicWithDeviceAddress(_deviceAddress, UduGattUuid.TrackpadService, UduGattUuid.TrackpadCharacteristicUUID, (notifyAddress, notifyCharacteristic) =>
        {
            _state = States.None;
            string characteristicName;
            UduGattUuid.Lookup.TryGetValue(notifyCharacteristic, out characteristicName);
            StatusMessage = "Subscribed to: " + characteristicName;
        }, (address, characteristicUUID, bytes) =>
        {
            if (_state != States.None)
            {
                // some devices do not properly send the notification state change which calls
                // the lambda just above this one so in those cases we don't have a great way to
                // set the state other than waiting until we actually got some data back.
                // The esp32 sends the notification above, but if yuor device doesn't you would have
                // to send data like pressing the button on the esp32 as the sketch for this demo
                // would then send data to trigger this.
                StatusMessage = "Waiting for user action (2)...";

                _state = States.None;
            }
            bleDataStream.SetTrackpadBytes(bytes);
        });
    }

    private void SubscribeToIMUCharacteristic()
    {
        BluetoothLEHardwareInterface.SubscribeCharacteristicWithDeviceAddress(_deviceAddress, UduGattUuid.IMUServiceUUID, UduGattUuid.IMUDataCharacteristicUUID, (notifyAddress, notifyCharacteristic) =>
        {
            StatusMessage = "Subscribed to IMU characteristic.";

        }, (address, characteristicUUID, bytes) =>
        {
            if (_state != States.None)
            {
                // some devices do not properly send the notification state change which calls
                // the lambda just above this one so in those cases we don't have a great way to
                // set the state other than waiting until we actually got some data back.
                // The esp32 sends the notification above, but if yuor device doesn't you would have
                // to send data like pressing the button on the esp32 as the sketch for this demo
                // would then send data to trigger this.
                StatusMessage = "Waiting for user action (2)...";

                _state = States.None;

            }
            bleDataStream.SetIMUBytes(bytes);
        });
    }

    private void SubscribeToEdgeImpulseCharacteristic()
    {
        BluetoothLEHardwareInterface.SubscribeCharacteristicWithDeviceAddress(_deviceAddress, UduGattUuid.GestureRecognitionServiceUUID, UduGattUuid.GestureRecognitionCharacteristicUUID, (notifyAddress, notifyCharacteristic) =>
        {
            string characteristicName;
            UduGattUuid.Lookup.TryGetValue(notifyCharacteristic, out characteristicName);
            StatusMessage = "Subscribed to: Edge Impulse "/* + characteristicName*/;
        }, (address, characteristicUUID, bytes) =>
        {

            if (_state != States.None)
            {
                // some devices do not properly send the notification state change which calls
                // the lambda just above this one so in those cases we don't have a great way to
                // set the state other than waiting until we actually got some data back.
                // The esp32 sends the notification above, but if yuor device doesn't you would have
                // to send data like pressing the button on the esp32 as the sketch for this demo
                // would then send data to trigger this.
                StatusMessage = "Waiting for user action (2)...";

                _state = States.None;
            }
            bleDataStream.SetEdgeImpluseBytes(bytes);
        });
    }
    #endregion

    #region Public Methods: Core

    #region Methods : LEDs
    public void SetLEDConstantColor(Color color, int brightness)
    {
        byte[] command = new byte[5];

        byte ledModeCmd = Convert.ToByte((short)LEDMode.ON);
        byte r = Convert.ToByte(color.r * 255);
        byte g = Convert.ToByte(color.g * 255);
        byte b = Convert.ToByte(color.b * 255);
        byte brightnessByte = Convert.ToByte(brightness);

        command[0] = ledModeCmd;
        command[1] = r;
        command[2] = g;
        command[3] = b;
        command[4] = brightnessByte;

        WriteCharacteristic(UduGattUuid.LEDServiceUUID, UduGattUuid.LEDPatternCharacteristicUUID, command);
    }

    public void SetLEDFlashingColor(Color color, int brightness, short flashingInterval, int durationInSeconds) //TODO: Is this needed with SetLED
    {
        byte[] command = new byte[7];

        byte ledModeCmd = Convert.ToByte((short)LEDMode.FLASH);
        byte r = Convert.ToByte(color.r * 255);
        byte g = Convert.ToByte(color.g * 255);
        byte b = Convert.ToByte(color.b * 255);
        byte brightnessByte = Convert.ToByte(brightness);
        byte[] flashingIntervalBytes = BitConverter.GetBytes(flashingInterval);

        command[0] = ledModeCmd;
        command[1] = r;
        command[2] = g;
        command[3] = b;
        command[4] = brightnessByte;
        command[5] = flashingIntervalBytes[0];
        command[6] = flashingIntervalBytes[1];

        WriteCharacteristic(UduGattUuid.LEDServiceUUID, UduGattUuid.LEDPatternCharacteristicUUID, command);

        //turn off current led
        BluetoothLEHardwareInterface.WriteCharacteristic(_deviceAddress, UduGattUuid.LEDServiceUUID, UduGattUuid.LEDPatternCharacteristicUUID, command, 1, true, (ledOffMessage) =>
        {
            StatusMessage = "LED power off";
            //set new led color
            BluetoothLEHardwareInterface.WriteCharacteristic(_deviceAddress, UduGattUuid.LEDServiceUUID, UduGattUuid.LEDPatternCharacteristicUUID, command, command.Length, true, (ledMessageA) =>
            {
                //StatusMessage = "LED color set to: " + ledColor.ToString();
                StartCoroutine(SetLEDOffAfterDelay(durationInSeconds));
            });
        });
    }

    public void SetLEDOff()
    {
        byte command = 0x00;
        WriteCharacteristic(UduGattUuid.LEDServiceUUID, UduGattUuid.LEDPatternCharacteristicUUID, command);
    }

    public IEnumerator SetLEDOffAfterShortDelay()
    {
        yield return new WaitForSeconds(0.25f);
        byte command = 0x00;
        WriteCharacteristic(UduGattUuid.LEDServiceUUID, UduGattUuid.LEDPatternCharacteristicUUID, command);
    }

    public IEnumerator SetLEDOffAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        byte command = 0x00;
        WriteCharacteristic(UduGattUuid.LEDServiceUUID, UduGattUuid.LEDPatternCharacteristicUUID, command);
    }

    public void SetLED(bool isFlashing, int r, int g, int b, int brightness, int durationInSeconds)
    {
        byte[] command = new byte[6];

        command[0] = Convert.ToByte(isFlashing);
        command[1] = Convert.ToByte(r);
        command[2] = Convert.ToByte(g);
        command[3] = Convert.ToByte(b);
        command[4] = Convert.ToByte(brightness);
        command[5] = Convert.ToByte(durationInSeconds);

        WriteCharacteristic(UduGattUuid.LEDServiceUUID, UduGattUuid.LEDPatternCharacteristicUUID, command);

        //turn off current led TODO: is it needed to turn off the current LED??
        BluetoothLEHardwareInterface.WriteCharacteristic(_deviceAddress, UduGattUuid.LEDServiceUUID, UduGattUuid.LEDPatternCharacteristicUUID, command, 1, true, (ledOffMessage) =>
        {
            StatusMessage = "LED power off";
            //set new led color
            BluetoothLEHardwareInterface.WriteCharacteristic(_deviceAddress, UduGattUuid.LEDServiceUUID, UduGattUuid.LEDPatternCharacteristicUUID, command, command.Length, true, (ledMessageA) =>
            {
                //StatusMessage = "LED color set to: " + ledColor.ToString();
                StartCoroutine(SetLEDOffAfterDelay(durationInSeconds));
            });
        });
    }
    #endregion

    #region Methods : Display
    // DISPLAY
    public void SetImage(string filename)
    {
        byte[] filenameAsBytes = Encoding.ASCII.GetBytes(filename);

        WriteCharacteristic(UduGattUuid.DisplayServiceUUID, UduGattUuid.DisplaySelectFileCharacteristicUUID, filenameAsBytes);
    }
    #endregion

    #region Methods : Haptics
    public void SetVibration(string filename)
    {
        byte[] filenameAsBytes = Encoding.ASCII.GetBytes(filename);
        WriteCharacteristic(UduGattUuid.VibrationServiceUUID, UduGattUuid.VibrationSelectFileCharacteristicUUID, filenameAsBytes);
    }

    public void SetAmplitude(int amplitude)
    {
        amplitude = Mathf.Clamp(amplitude, 0, 100);
        byte command = Convert.ToByte(amplitude);

        WriteCharacteristic(UduGattUuid.VibrationServiceUUID, UduGattUuid.VibrationAmplitudeCharacteristicUUID, command);
    }

    public void StartVibration()
    {
        byte command = 0x01;
        WriteCharacteristic(UduGattUuid.VibrationServiceUUID, UduGattUuid.VibrationStartStopCharacteristicUUID, command);
    }

    public void StopVibration()
    {
        byte data = 0x00;

        WriteCharacteristic(UduGattUuid.VibrationServiceUUID, UduGattUuid.VibrationStartStopCharacteristicUUID, data);
    }

    public void SetVibrationAndStart(string filename)
    {
        if (currentVibrationFilename == filename)
        {
            StartVibration();
        }
        else
        {
            currentVibrationFilename = filename;
            byte[] data = Encoding.ASCII.GetBytes(filename);
            int length = data.Length;

            BluetoothLEHardwareInterface.WriteCharacteristic(_deviceAddress, UduGattUuid.VibrationServiceUUID, UduGattUuid.VibrationSelectFileCharacteristicUUID, data, length, true, (sometext) =>
            {
                StartVibration();
            });
        }
    }
    #endregion

    #region Methods : Gesture Recognition
    public void StartGestureRecognition()
    {
        byte[] data = new byte[3];

        data[0] = 0x01; // trigger button
        //data[0] = 0x02; // squeeze button

        data[1] = 0x05;
        data[2] = 0xdc;

        WriteCharacteristic(UduGattUuid.GestureRecognitionServiceUUID, UduGattUuid.GestureRecognitionCharacteristicUUID, data);
        Debug.Log("starting gesture recognition ");
    }

    public void StopGestureRecognition()
    {
        byte data = 0x00;
        WriteCharacteristic(UduGattUuid.GestureRecognitionServiceUUID, UduGattUuid.GestureRecognitionCharacteristicUUID, data);
        Debug.Log("stopping gesture recognition ");
    }
    #endregion

    #region Methods : Multi Methods
    public void SetImageVibrationAndLEDs(string imageName, string vibrationName, Color ledColor)
    {
        // led setup
        byte[] ledOffCmd = { 0x00 };

        byte[] ledCommand = new byte[5];

        byte ledModeCmd = Convert.ToByte((short)LEDMode.ON);
        byte r = Convert.ToByte(ledColor.r * 255);
        byte g = Convert.ToByte(ledColor.g * 255);
        byte b = Convert.ToByte(ledColor.b * 255);
        byte brightnessByte = Convert.ToByte(ledColor.a * 255);

        ledCommand[0] = ledModeCmd;
        ledCommand[1] = r;
        ledCommand[2] = g;
        ledCommand[3] = b;
        ledCommand[4] = brightnessByte;

        // display setup
        byte[] filenameBytes = Encoding.ASCII.GetBytes(imageName);

        //turn off current led
        BluetoothLEHardwareInterface.WriteCharacteristic(_deviceAddress, UduGattUuid.LEDServiceUUID, UduGattUuid.LEDPatternCharacteristicUUID, ledOffCmd, 1, true, (ledOffMessage) =>
        {
            StatusMessage = "LED power off";
            //set new led color
            BluetoothLEHardwareInterface.WriteCharacteristic(_deviceAddress, UduGattUuid.LEDServiceUUID, UduGattUuid.LEDPatternCharacteristicUUID, ledCommand, ledCommand.Length, true, (ledMessageA) =>
            {
                StatusMessage = "LED color set to: " + ledColor.ToString();
                // set display image to gif
                BluetoothLEHardwareInterface.WriteCharacteristic(_deviceAddress, UduGattUuid.DisplayServiceUUID, UduGattUuid.DisplaySelectFileCharacteristicUUID, filenameBytes, filenameBytes.Length, true, (displayMessage) =>
                {
                    Debug.Log("Update image: " + displayMessage);
                    SetVibrationAndStart(vibrationName);
                });
            });
        });
    }

    public void SetImageAndLEDs(string imageName, Color ledColor)
    {
        // led setup
        byte[] ledOffCmd = { 0x00 };

        byte[] ledCommand = new byte[5];

        byte ledModeCmd = Convert.ToByte((short)LEDMode.ON);
        byte r = Convert.ToByte(ledColor.r * 255);
        byte g = Convert.ToByte(ledColor.g * 255);
        byte b = Convert.ToByte(ledColor.b * 255);
        byte brightnessByte = Convert.ToByte(ledColor.a * 255);

        ledCommand[0] = ledModeCmd;
        ledCommand[1] = r;
        ledCommand[2] = g;
        ledCommand[3] = b;
        ledCommand[4] = brightnessByte;

        // display setup
        byte[] filenameBytes = Encoding.ASCII.GetBytes(imageName);

        //turn off current led
        BluetoothLEHardwareInterface.WriteCharacteristic(_deviceAddress, UduGattUuid.LEDServiceUUID, UduGattUuid.LEDPatternCharacteristicUUID, ledOffCmd, 1, true, (ledOffMessage) =>
        {
            StatusMessage = "LED power off";
            //set new led color
            BluetoothLEHardwareInterface.WriteCharacteristic(_deviceAddress, UduGattUuid.LEDServiceUUID, UduGattUuid.LEDPatternCharacteristicUUID, ledCommand, ledCommand.Length, true, (ledMessageA) =>
            {
                StatusMessage = "LED color set to: " + ledColor.ToString();
                // set display image to gif
                BluetoothLEHardwareInterface.WriteCharacteristic(_deviceAddress, UduGattUuid.DisplayServiceUUID, UduGattUuid.DisplaySelectFileCharacteristicUUID, filenameBytes, filenameBytes.Length, true, (displayMessage) =>
                {

                });
            });
        });
    }

    public void StartVibrationAndLEDs(string vibrationName, Color ledColor)
    {
        // led setup
        byte[] ledOffCmd = { 0x00 };

        byte[] ledCommand = new byte[5];

        byte ledModeCmd = Convert.ToByte((short)LEDMode.ON);
        byte r = Convert.ToByte(ledColor.r * 255);
        byte g = Convert.ToByte(ledColor.g * 255);
        byte b = Convert.ToByte(ledColor.b * 255);
        byte brightnessByte = Convert.ToByte(ledColor.a * 255);

        ledCommand[0] = ledModeCmd;
        ledCommand[1] = r;
        ledCommand[2] = g;
        ledCommand[3] = b;
        ledCommand[4] = brightnessByte;

        //turn off current led
        BluetoothLEHardwareInterface.WriteCharacteristic(_deviceAddress, UduGattUuid.LEDServiceUUID, UduGattUuid.LEDPatternCharacteristicUUID, ledOffCmd, 1, true, (ledOffMessage) =>
        {
            StatusMessage = "LED power off";
            //set new led color
            BluetoothLEHardwareInterface.WriteCharacteristic(_deviceAddress, UduGattUuid.LEDServiceUUID, UduGattUuid.LEDPatternCharacteristicUUID, ledCommand, ledCommand.Length, true, (ledMessageA) =>
            {
                //set and start vibration
                SetVibrationAndStart(vibrationName);
            });
        });
    }

    public void StartVibrationAndSetImage(string vibrationName, string imageName)
    {
        // display setup
        byte[] filenameBytes = Encoding.ASCII.GetBytes(imageName);

        // set display image to gif
        BluetoothLEHardwareInterface.WriteCharacteristic(_deviceAddress, UduGattUuid.DisplayServiceUUID, UduGattUuid.DisplaySelectFileCharacteristicUUID, filenameBytes, filenameBytes.Length, true, (displayMessage) =>
        {
            SetVibrationAndStart(vibrationName);
        });
    }
    #endregion

    #endregion

    #region Initial Setup Methods: Seqeuences
    //Connected
    public bool isConnected()
    {
        return Connected;
    }

    public void InitializeUDUConsole()
    {
        Thread.Sleep(200);
        SubscribeToIMUCharacteristic();
        Thread.Sleep(300);
        SubscribeToButtonCharacteristic();
        Thread.Sleep(300);
        SubscribeToTrackpadCharacteristic();
        Thread.Sleep(300);
        SubscribeToEdgeImpulseCharacteristic();
        Thread.Sleep(300);
        SetAmplitude(100);

        SetImageVibrationAndLEDs("/spiffs/snowballdisplay.gif", "/spiffs/Fruit150.wav", Color.green);

        Connected = true;
        ConsoleIntegration.Instance.isConnected = true;
    }
    #endregion

    #region Helpers
    public enum States
    {
        None,
        Scan,
        ScanRSSI,
        Connect,
        RequestMTU,
        Subscribe,
        Unsubscribe,
        Disconnect,
    }
    void Reset()
    {
        if (textLog != null)
        {
            textLog.text = string.Empty;
        }
        Connected = false;
        _timeout = 0f;
        _state = States.None;
        _deviceAddress = null;
        _rssi = 0;
    }
    private void WriteCharacteristic(string _serviceUUID, string _characteristicUUID, byte[] _data)
    {
        int _length = _data.Length;

        BluetoothLEHardwareInterface.WriteCharacteristic(_deviceAddress, _serviceUUID, _characteristicUUID, _data, _length, true, (sometext) =>
        {
            StatusMessage = $"Char: {UduGattUuid.Lookup[sometext.ToLower()]} recieved command: {BitConverter.ToString(_data)} ";
            StatusMessage = "Message: " + sometext;
        });
    }

    private void EnsureWriteCharacteristic(string _serviceUUID, string _characteristicUUID, byte[] _data, Action<string> callbackMessage)
    {
        int _length = _data.Length;

        BluetoothLEHardwareInterface.WriteCharacteristic(_deviceAddress, _serviceUUID, _characteristicUUID, _data, _length, true, (sometext) =>
        {
            StatusMessage = $"Char: {UduGattUuid.Lookup[sometext.ToLower()]} recieved command: {BitConverter.ToString(_data)} ";
            callbackMessage(sometext);
        });
    }

    private void WriteCharacteristic(string _serviceUUID, string _characteristicUUID, byte _data)
    {
        byte[] data = { _data };
        int _length = 1;

        BluetoothLEHardwareInterface.WriteCharacteristic(_deviceAddress, _serviceUUID, _characteristicUUID, data, _length, true, (sometext) =>
        {
            StatusMessage = $"Characteristic {UduGattUuid.Lookup[sometext.ToLower()]} recieved command: {data[0]} ";
            StatusMessage = "Message: " + sometext;
        });
    }

    void CallbackFromWriteCommand(string response)
    {
        Debug.Log(response);
    }

    #endregion

    #region Core

    public void SetBLEDataStream(BLEDataStream dataStream)
    {
        bleDataStream = dataStream;
    }
    public void SetState(States newState, float timeout)
    {
        _state = newState;
        _timeout = timeout;
    }

    bool IsEqual(string uuid1, string uuid2)
    {
        return (uuid1.ToUpper().Equals(uuid2.ToUpper()));
    }

    string FullUUID(string uuid)
    {
        string fullUUID = uuid;
        if (fullUUID.Length == 4)
            fullUUID = "0000" + uuid + "-0000-1000-8000-00805f9b34fb";

        return fullUUID;
    }

    void OnSubscribtionComplete(string characteristic)
    {
        StatusMessage = $"Event: Subscribed to {UduGattUuid.Lookup[characteristic]}";
    }

    private string StatusMessage
    {
        set
        {
            //BluetoothLEHardwareInterface.Log(value);
            Debug.Log(value);
            if (textLog == null) return;
            textLog.text += "\n" + value;
        }
    }

    private void OnDestroy()
    {
        SetState(States.Unsubscribe, 5f);
    }
    #endregion
}