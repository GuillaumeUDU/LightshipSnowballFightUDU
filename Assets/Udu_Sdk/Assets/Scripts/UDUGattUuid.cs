using System.Collections.Generic;

public static class UduGattUuid
{
    static public Dictionary<string, string> Lookup;

    // Services
    // UDU PoC does not support simultaneous IMUService and GestureRecognitionService
    public static string IMUServiceUUID = "617436dc-8daf-11ec-b909-0242ac120002";
    public static string BatteryServiceUUID = "c661737d-6f90-4358-9728-bc4c0ed3ddff";
    public static string ButtonsServiceUUID = "073e44a6-12f2-4733-83c3-2ebac632d0ac";
    public static string CRUDServiceUUID = "e8e25616-9251-4a4c-a6f9-8d517347cafe";
    public static string VibrationServiceUUID = "581b3a03-5b4d-40cf-adc1-0232b47f4962";
    public static string DisplayServiceUUID = "e722b8a2-8303-4a78-8d75-be25fd0671aa";
    public static string LEDServiceUUID = "1900ba5f-d841-4ae7-86a8-69538f40e3df";
    public static string TrackpadService = "bd7da8ce-86a5-11ed-a1eb-0242ac120002";
    public static string GestureRecognitionServiceUUID = "4e6181a6-b9b6-11ed-afa1-0242ac120002";

    //Characteristics
    // Write = set values
    // Read = get values
    // Notify = subscribe to stream of continuous values

    // Inertial Measurement Unit (IMU) characteristics
    public static string IMUDataCharacteristicUUID = "26b2eb77-8e13-438e-b9e0-91e85d8a2814"; //Notify
    public static string IMUCalibrationCharacteristicUUID = "db520e3a-d9b0-4eef-aedd-41521297dc9b"; //Read, Write
    public static string IMUConfigurationCharacteristicUUID = "b019dc61-6844-4810-933f-ff6900529a33"; //Write

    // Gesture recognition characteristics
    public static string GestureRecognitionCharacteristicUUID = "4e61844e-b9b6-11ed-afa1-0242ac120002"; //Notify, write

    // Battery characteristics
    public static string BatteryLevelCharacteristicUUID = "a3991f56-eb2b-4d79-80a6-3491842f6d67"; //Read, Notify
    public static string BatteryChargingStatusCharacteristicUUID = "fe481bf5-f5ed-4568-b43b-2accdcc29ad7"; //Read, Notify

    // Button characteristics
    public static string ButtonEventCharacteristicUUID = "b2c333b0-e819-4ce9-8ddf-8fcb29e42311"; //Notify

    // Create, Read, Update, Delte (CRUD) characteristics
    public static string CRUDCommandCharacteristicUUID = "a578fb86-042f-4daa-9293-030a71898895"; //Write, Notify
    public static string CRUDDataCharacteristicUUID = "4f5f0685-9dc0-434a-ab8c-80b91ccd736a"; //Write, Notify

    // Vibration characteristics
    public static string VibrationSelectFileCharacteristicUUID = "4d7c34ed-0572-4b82-92f4-115202b257fa"; //Write
    public static string VibrationStartStopCharacteristicUUID = "56a0ab24-e5de-44e4-82cf-8fed9f1b4dcc"; //Write
    public static string VibrationAmplitudeCharacteristicUUID = "5d364215-d225-4dac-b94f-6e1576c91ff5"; //Write, Read

    // Display characteristics
    public static string DisplaySelectFileCharacteristicUUID = "1e2463f0-ea34-4772-9d73-ecc5cc5fa435"; //Write
    public static string DisplayClearCharacteristicUUID = "8606e934-6f1f-43d0-8655-38c094dd7e49"; //Write
    public static string DisplayBrightnessCharacteristicUUID = "b29f5f61-b65d-4ab9-bfeb-29e3d60c6db4"; //Write, Read

    // Light-emitting diode (LED) characteristics
    public static string LEDPatternCharacteristicUUID = "61eff8ad-4755-4790-85f0-730e97444c71"; //Write

    //Trackpad Service characteristics
    public static string TrackpadCharacteristicUUID = "f7f3345a-86a6-11ed-a1eb-0242ac120002"; //Write

    static UduGattUuid()
    {
        Lookup = new Dictionary<string, string>()
            {
                { "617436dc-8daf-11ec-b909-0242ac120002", "IMU Service" },
                { "c661737d-6f90-4358-9728-bc4c0ed3ddff", "Battery Service" },
                { "073e44a6-12f2-4733-83c3-2ebac632d0ac", "Buttons Service" },
                { "e8e25616-9251-4a4c-a6f9-8d517347cafe", "CRUD Service" },
                { "4e6181a6-b9b6-11ed-afa1-0242ac120002", "Gesture Service" },
                { "581b3a03-5b4d-40cf-adc1-0232b47f4962", "Vibration Service" },
                { "e722b8a2-8303-4a78-8d75-be25fd0671aa", "Display Service" },
                { "1900ba5f-d841-4ae7-86a8-69538f40e3df", "LED Service" },
                { "bd7da8ce-86a5-11ed-a1eb-0242ac120002", "Trackpad Service" },

                { "b019dc61-6844-4810-933f-ff6900529a33", "IMU Configuration Characteristic" },
                { "26b2eb77-8e13-438e-b9e0-91e85d8a2814", "IMU Data Characteristic" },
                { "db520e3a-d9b0-4eef-aedd-41521297dc9b", "IMU Calibration Status Characteristic" },
                { "a3991f56-eb2b-4d79-80a6-3491842f6d67", "Battery Level Characteristic" },
                { "fe481bf5-f5ed-4568-b43b-2accdcc29ad7", "Battery Charging Status Characteristic" },
                { "b2c333b0-e819-4ce9-8ddf-8fcb29e42311", "Button Event Characteristic" },
                { "a578fb86-042f-4daa-9293-030a71898895", "CRUD Command Characteristic" },
                { "4f5f0685-9dc0-434a-ab8c-80b91ccd736a", "CRUD Data Characteristic" },
                { "4e61844e-b9b6-11ed-afa1-0242ac120002", "Gesture recognition" },
                { "4d7c34ed-0572-4b82-92f4-115202b257fa", "Vibration Set Characteristic" },
                { "56a0ab24-e5de-44e4-82cf-8fed9f1b4dcc", "Vibration Start Stop Characteristic" },
                { "5d364215-d225-4dac-b94f-6e1576c91ff5", "Vibration Amplitude Characteristic" },
                { "1e2463f0-ea34-4772-9d73-ecc5cc5fa435", "Display Set Image Characteristic" },
                { "8606e934-6f1f-43d0-8655-38c094dd7e49", "Display Clear Characteristic" },
                { "b29f5f61-b65d-4ab9-bfeb-29e3d60c6db4", "Display Brightness Characteristic" },
                { "61eff8ad-4755-4790-85f0-730e97444c71", "LED Pattern Characteristic" },
                { "f7f3345a-86a6-11ed-a1eb-0242ac120002", "Trackpad Characteristics" }
            };
    }
}