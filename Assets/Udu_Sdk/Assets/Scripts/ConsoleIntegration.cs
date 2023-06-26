using System.Collections;
using UnityEngine;

public class ConsoleIntegration : Singleton<ConsoleIntegration>
{
    // Console integration
    [HideInInspector]
    public AbstractDataStream uduConsoleDatastream;
    public bool isConnected;

    private bool ConsoleIsConnected() => isConnected;
    private float intervalUpdate = 1f;

    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(LoadMain());
        uduConsoleDatastream = GetComponent<BLEDataStream>();
    }

    private IEnumerator LoadMain()
    {
        while (!isConnected)
        {
            intervalUpdate -= Time.deltaTime;

            if (intervalUpdate <= 0)
            {
                intervalUpdate = 1f;
                Debug.Log("console is trying to connect");
            }

            yield return null;
        }

        DisableCanvas();
    }

    //
    private void DisableCanvas()
    {
        GameObject connectingScreen = this.transform.Find("LoadingCanvas").gameObject;
        connectingScreen.SetActive(false);

        Debug.Log("console is connected");

        ModifyConsoleDisplay();
    }

    void ModifyConsoleDisplay()
    {
        uduConsoleDatastream.SetImageAndLEDs("/spiffs/strawberry.gif", Color.white);
    }
}