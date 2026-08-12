using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

[System.Serializable]
public class DetectResponse
{
    public string status;
    public bool face_forward;
    public float forward_duration;
    public string visitor_name;
    public float confidence;
    public string session_id;
    public string audio_base64;
    public string audio_key;
    public string answer_text;
}

public class FaceDetectionService : MonoBehaviour
{
    [Header("Backend")]
    // FIXED: Corrected double colon typo "::8000" to ":8000"
    public string baseUrl = "https://snub-tactics-impatient.ngrok-free.dev"; 

    [Header("References")]
    public WebCamTexture webcamTexture;
    public float pollInterval = 0.7f;   

    public System.Action<DetectResponse> OnDetectionResult;

    private bool isPolling = false;

    public void StartPolling()
    {   
        if (!isPolling)
        {
            isPolling = true;
            StartCoroutine(PollLoop());
            Debug.Log("Polling Started.");
        }
    }

    public void StopPolling()
    {
        isPolling = false;
        Debug.Log("Polling Stopped.");
    }

    IEnumerator PollLoop()
    {
        while (isPolling)
        {
            yield return StartCoroutine(SendFrame());
            yield return new WaitForSeconds(pollInterval);
        }
    }

    IEnumerator SendFrame()
    {
        if (webcamTexture == null || !webcamTexture.isPlaying)
            yield break;

        Color32[] pixels = webcamTexture.GetPixels32();

        Texture2D snap = new Texture2D(
            webcamTexture.width,
            webcamTexture.height,
            TextureFormat.RGB24,
            false);

        snap.SetPixels32(pixels);
        snap.Apply();

        Debug.Log($"Captured frame: {snap.width} x {snap.height}");
        byte[] jpegBytes = snap.EncodeToJPG(70);
        Destroy(snap);

        WWWForm form = new WWWForm();
        form.AddBinaryData("frame", jpegBytes, "frame.jpg", "image/jpeg");

        using (UnityWebRequest request = UnityWebRequest.Post($"{baseUrl}/detect", form))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                DetectResponse response = JsonUtility.FromJson<DetectResponse>(json);
                OnDetectionResult?.Invoke(response);
            }
            else
            {
                Debug.LogWarning("Detect request failed: " + request.error);
            }
        }
    }
}