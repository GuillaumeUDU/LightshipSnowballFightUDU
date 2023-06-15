using UnityEngine;
using UnityEngine.Android;

public class BluetoothPermissions : MonoBehaviour
{
#if UNITY_ANDROID
    private void Awake()
    {
        if (!Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_SCAN")
            ||
            !Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_CONNECT"))
        {
            Permission.RequestUserPermissions(new string[]
            {
                "android.permission.BLUETOOTH_SCAN",
                "android.permission.BLUETOOTH_CONNECT"
            });
        }
    }
#endif
}