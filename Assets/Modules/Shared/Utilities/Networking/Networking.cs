using System.Collections;

using UnityEngine;
using UnityEngine.Networking;

namespace Niantic.ARVoyage.Utilities
{
    /// <summary>
    /// Utility class for networking functions, currently provides a method for
    /// retrieving a remote texture and passing it into a callback.
    /// </summary>
    public class Networking
    {

        public static IEnumerator GetRemoteTextureRoutine(string url, System.Action<Texture2D> callback)
        {
            using (UnityWebRequest unityWebRequest = UnityWebRequestTexture.GetTexture(url))
            {
                yield return unityWebRequest.SendWebRequest();

                if (unityWebRequest.result != UnityWebRequest.Result.Success)
                {
                    Debug.Log(unityWebRequest.error);
                    callback(null);
                }
                else
                {
                    Texture2D texture = DownloadHandlerTexture.GetContent(unityWebRequest);
                    callback(texture);
                }
            }
        }
    }
}