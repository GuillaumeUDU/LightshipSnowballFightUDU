using UnityEngine;

public class ConsoleSampleTrackpadMovement : MonoBehaviour
{
    private float trackpadX, trackpadY, trackpadZ;

    private void Update()
    {
        GetConsoleData();
        CharacterMove();
    }

    private void GetConsoleData()
    {
        if (ConsoleIntegration.Instance.isConnected == true)
        {
            trackpadX = ConsoleIntegration.Instance.uduConsoleDatastream.GetTrackpadCoordinates().x;
            trackpadY = ConsoleIntegration.Instance.uduConsoleDatastream.GetTrackpadCoordinates().y;
            trackpadZ = ConsoleIntegration.Instance.uduConsoleDatastream.GetTrackpadCoordinates().z;
        }
    }

    // move in 8 directions
    private void CharacterMove()
    {
        if (trackpadX != 0 || trackpadY != 0)
        {
            if (trackpadX > 600f && trackpadY > 550f && trackpadY < 850f) // up
            {
                transform.position = transform.position + new Vector3(0f, 2f * Time.deltaTime, 0f);
            }
            else if (trackpadX < 600f && trackpadY > 550f && trackpadY < 850f) // down
            {
                transform.position = transform.position + new Vector3(0f, -2f * Time.deltaTime, 0f);
            }
            else if (trackpadY > 600f && trackpadX > 750f && trackpadX < 1300f) // right
            {
                transform.position = transform.position + new Vector3(2f * Time.deltaTime, 0f, 0f);
            }
            else if (trackpadY < 600f && trackpadX > 750f && trackpadX < 1300f) // left
            {
                transform.position = transform.position + new Vector3(-2f * Time.deltaTime, 0f, 0f);
            }

            else if (trackpadX > 600f && trackpadY > 800f && trackpadY < 1150f) // up right
            {
                transform.position = transform.position + new Vector3(2f * Time.deltaTime, 2f * Time.deltaTime, 0f);
            }
            else if (trackpadX > 600f && trackpadY < 650f && trackpadY > 300f) // up lefts
            {
                transform.position = transform.position + new Vector3(-2f * Time.deltaTime, 2f * Time.deltaTime, 0f);
            }

            else if (trackpadY > 600f && trackpadX > 400f && trackpadX < 750f) // down right
            {
                transform.position = transform.position + new Vector3(2f * Time.deltaTime, -2f * Time.deltaTime, 0f);
            }
            else if (trackpadY < 600f && trackpadX > 400f && trackpadX < 750f) // down left
            {
                transform.position = transform.position + new Vector3(-2f * Time.deltaTime, -2f * Time.deltaTime, 0f);
            }
        }
    }
}