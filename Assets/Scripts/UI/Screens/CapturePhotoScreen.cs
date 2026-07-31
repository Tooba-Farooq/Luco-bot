using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;

public class CapturePhotoScreen : MonoBehaviour
{
    [Header("UI References")]
    public RawImage cameraPreview;
    public GameObject promptText;
    public GameObject retryText; // NEW - shown briefly on 409 failure

    [Header("Boundary Outline")]
    public GameObject boundaryOutline;
    public Color detectedColor = Color.green;
    public Color undetectedColor = Color.red;

    [Header("Dependencies")]
    public DeviceCheck deviceCheck;
    public VisitorFlowManager flowManager;
    public FaceExpressionController face;
    public FaceDetectionService detectionService; // only used for baseUrl now

    [Header("Audio")]
    public AudioClip lookAtCameraClip;

    [Header("Polling")]
    public float pollInterval = 0.3f;

    private bool isPolling = false;
    private bool captureInFlight = false; // guards against double-firing capture-photo

    void OnEnable()
    {   RobotCaptionController.Instance.SetSuppressed(true);
        if (face != null && lookAtCameraClip != null)
            face.StartTalking(lookAtCameraClip);

        ResetScreen();
        StartPolling();
    }

    void OnDisable()
    {
        RobotCaptionController.Instance.SetSuppressed(false);
        StopPolling();
    }

    void Update()
    {
        if (cameraPreview.gameObject.activeSelf && deviceCheck.camTexture != null)
        {
            cameraPreview.texture = deviceCheck.camTexture;
        }
    }

    // ---------- Polling /session/photo-frame ----------

    private void StartPolling()
    {
        if (!isPolling)
        {
            isPolling = true;
            StartCoroutine(PollLoop());
        }
    }

    private void StopPolling()
    {
        isPolling = false;
    }

    private IEnumerator PollLoop()
    {
        while (isPolling)
        {
            if (!captureInFlight)
                yield return StartCoroutine(SendPhotoFrame());

            yield return new WaitForSeconds(pollInterval);
        }
    }

    private IEnumerator SendPhotoFrame()
    {
        if (deviceCheck.camTexture == null || !deviceCheck.camTexture.isPlaying)
            yield break;

        byte[] jpegBytes = CaptureCurrentFrameAsJpeg();
        if (jpegBytes == null) yield break;

        WWWForm form = new WWWForm();
        form.AddField("session_id", SessionManager.Instance.CurrentSessionId);
        form.AddBinaryData("frame", jpegBytes, "frame.jpg", "image/jpeg");

        using (UnityWebRequest request = UnityWebRequest.Post($"{detectionService.baseUrl}/session/photo-frame", form))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                PhotoFrameResponse response = JsonUtility.FromJson<PhotoFrameResponse>(request.downloadHandler.text);
                UpdateBoundaryVisual(response);

                if (response.ready_to_capture && !captureInFlight)
                {
                    captureInFlight = true;
                    StopPolling();
                    StartCoroutine(CapturePhoto(jpegBytes)); // reuse the same frame that triggered readiness
                }
            }
            else
            {
                Debug.LogWarning("photo-frame poll failed: " + request.error);
            }
        }
    }

    private byte[] CaptureCurrentFrameAsJpeg()
    {
        WebCamTexture cam = deviceCheck.camTexture;
        Texture2D snap = new Texture2D(cam.width, cam.height, TextureFormat.RGB24, false);
        snap.SetPixels32(cam.GetPixels32());
        snap.Apply();

        byte[] jpegBytes = snap.EncodeToJPG(70);
        Destroy(snap);
        return jpegBytes;
    }

    private void UpdateBoundaryVisual(PhotoFrameResponse response)
    {
        bool good = response.face_found && response.is_forward && response.is_centered;

        if (boundaryOutline != null)
        {
            Color targetColor = good ? detectedColor : undetectedColor;
            foreach (Graphic childGraphic in boundaryOutline.GetComponentsInChildren<Graphic>())
                childGraphic.color = targetColor;
        }
    }

    // ---------- /session/capture-photo ----------

    private IEnumerator CapturePhoto(byte[] jpegBytes)
    {
        if (promptText != null) promptText.SetActive(false);
        if (retryText != null) retryText.SetActive(false);

        WWWForm form = new WWWForm();
        form.AddField("session_id", SessionManager.Instance.CurrentSessionId);
        form.AddBinaryData("frame", jpegBytes, "frame.jpg", "image/jpeg");

        using (UnityWebRequest request = UnityWebRequest.Post($"{detectionService.baseUrl}/session/capture-photo", form))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                SessionResponse response = JsonUtility.FromJson<SessionResponse>(request.downloadHandler.text);
                Debug.Log("capture-photo success, state: " + response.state);

                if (face != null) face.SetExpression(FaceExpression.Success);

                yield return SessionManager.Instance.PlayResponseAudio(response);

                // Expect READY_FOR_HANDOFF here per the README, but fall back safely either way
                if (response.state == "READY_FOR_HANDOFF")
                    flowManager.GoTo(VisitorFlowState.MeetSomeone_ShowSimilarNames);
                else
                    flowManager.GoTo(VisitorFlowState.MeetSomeone_ShowSimilarNames); // safe default terminal step

                captureInFlight = false;
            }
            else if (request.responseCode == 409)
            {
                Debug.LogWarning("capture-photo rejected (409): " + request.downloadHandler.text);
                if (retryText != null) retryText.SetActive(true);
                if (promptText != null) promptText.SetActive(true);

                captureInFlight = false;
                StartPolling(); // resume polling for another attempt
            }
            else
            {
                Debug.LogWarning("capture-photo failed: " + request.error);
                captureInFlight = false;
                StartPolling(); // don't get stuck — just retry
            }
        }
    }

    // ---------- Reset ----------

    public void ResetScreen()
    {
        captureInFlight = false;
        cameraPreview.gameObject.SetActive(true);

        if (boundaryOutline != null)
        {
            boundaryOutline.SetActive(true);
            foreach (Graphic childGraphic in boundaryOutline.GetComponentsInChildren<Graphic>())
                childGraphic.color = undetectedColor;
        }

        if (promptText != null) promptText.SetActive(true);
        if (retryText != null) retryText.SetActive(false);
    }
}