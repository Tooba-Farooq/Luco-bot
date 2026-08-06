using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;

public class CapturePhotoScreen : MonoBehaviour
{
    [Header("UI References")]
    public RawImage cameraPreview;
    public GameObject promptText;
    public GameObject retryText;

    [Header("Boundary Outline")]
    public GameObject boundaryOutline;
    public Color detectedColor = Color.green;
    public Color undetectedColor = Color.red;

    [Header("Dependencies")]
    public DeviceCheck deviceCheck;
    public VisitorFlowManager flowManager;
    public FaceExpressionController face;
    public FaceDetectionService detectionService;

    [Header("Audio")]
    public AudioClip lookAtCameraClip;

    [Header("Polling")]
    public float pollInterval = 0.3f;

    private bool isPolling = false;
    private bool captureInFlight = false;
    private Coroutine pollingCoroutine = null;


    // =========================================================
    // ENABLE
    // =========================================================

    void OnEnable()
    {
        Debug.Log("[CapturePhotoScreen] ENABLED");

        if (RobotCaptionController.Instance != null)
        {
            RobotCaptionController.Instance.SetSuppressed(true);
        }

        if (face != null && lookAtCameraClip != null)
        {
            face.StartTalking(lookAtCameraClip);
        }

        ResetScreen();

        StartPolling();
    }


    // =========================================================
    // DISABLE
    // =========================================================

    void OnDisable()
    {
        Debug.Log("[CapturePhotoScreen] DISABLED");

        if (RobotCaptionController.Instance != null)
        {
            RobotCaptionController.Instance.SetSuppressed(false);
        }

        StopPolling();

        // Important:
        // If this screen is left while a capture request is running,
        // don't allow its completion to transition the UI later.
        captureInFlight = false;
    }


    // =========================================================
    // UPDATE
    // =========================================================

    void Update()
    {
        if (cameraPreview != null &&
            cameraPreview.gameObject.activeSelf &&
            deviceCheck != null &&
            deviceCheck.camTexture != null)
        {
            cameraPreview.texture =
                deviceCheck.camTexture;
        }
    }


    // =========================================================
    // START POLLING
    // =========================================================

    private void StartPolling()
    {
        if (isPolling)
            return;

        if (!isActiveAndEnabled)
            return;

        isPolling = true;

        pollingCoroutine =
            StartCoroutine(PollLoop());
    }


    // =========================================================
    // STOP POLLING
    // =========================================================

    private void StopPolling()
    {
        isPolling = false;

        if (pollingCoroutine != null)
        {
            StopCoroutine(pollingCoroutine);
            pollingCoroutine = null;
        }
    }


    // =========================================================
    // POLL LOOP
    // =========================================================

    private IEnumerator PollLoop()
    {
        while (isPolling)
        {
            if (!captureInFlight)
            {
                yield return StartCoroutine(
                    SendPhotoFrame()
                );
            }

            yield return new WaitForSeconds(
                pollInterval
            );
        }

        pollingCoroutine = null;
    }


    // =========================================================
    // SEND PHOTO FRAME
    // =========================================================

    private IEnumerator SendPhotoFrame()
    {
        if (!isActiveAndEnabled)
            yield break;

        if (deviceCheck == null ||
            deviceCheck.camTexture == null ||
            !deviceCheck.camTexture.isPlaying)
        {
            yield break;
        }

        if (SessionManager.Instance == null)
            yield break;

        if (detectionService == null)
            yield break;

        byte[] jpegBytes =
            CaptureCurrentFrameAsJpeg();

        if (jpegBytes == null)
            yield break;

        WWWForm form =
            new WWWForm();

        form.AddField(
            "session_id",
            SessionManager.Instance.CurrentSessionId
        );

        form.AddBinaryData(
            "frame",
            jpegBytes,
            "frame.jpg",
            "image/jpeg"
        );

        string url =
            $"{detectionService.baseUrl}/session/photo-frame";

        using (
            UnityWebRequest request =
                UnityWebRequest.Post(url, form)
        )
        {
            yield return request.SendWebRequest();

            // Screen may have changed while request was running.
            if (!isActiveAndEnabled)
                yield break;

            if (
                request.result ==
                UnityWebRequest.Result.Success
            )
            {
                PhotoFrameResponse response =
                    JsonUtility.FromJson<
                        PhotoFrameResponse
                    >(
                        request.downloadHandler.text
                    );

                if (response == null)
                {
                    Debug.LogWarning(
                        "[CapturePhotoScreen] " +
                        "Invalid photo-frame response."
                    );

                    yield break;
                }

                UpdateBoundaryVisual(response);

                if (
                    response.ready_to_capture &&
                    !captureInFlight
                )
                {
                    captureInFlight = true;

                    StopPolling();

                    Debug.Log(
                        "[CapturePhotoScreen] " +
                        "Face ready. Capturing photo."
                    );

                    StartCoroutine(
                        CapturePhoto(jpegBytes)
                    );
                }
            }
            else
            {
                Debug.LogWarning(
                    "[CapturePhotoScreen] " +
                    "photo-frame poll failed: " +
                    request.error
                );
            }
        }
    }


    // =========================================================
    // CAPTURE CURRENT CAMERA FRAME
    // =========================================================

    private byte[] CaptureCurrentFrameAsJpeg()
    {
        if (deviceCheck == null ||
            deviceCheck.camTexture == null)
        {
            return null;
        }

        WebCamTexture cam =
            deviceCheck.camTexture;

        if (cam.width <= 16 ||
            cam.height <= 16)
        {
            return null;
        }

        Texture2D snap =
            new Texture2D(
                cam.width,
                cam.height,
                TextureFormat.RGB24,
                false
            );

        snap.SetPixels32(
            cam.GetPixels32()
        );

        snap.Apply();

        byte[] jpegBytes =
            snap.EncodeToJPG(70);

        Destroy(snap);

        return jpegBytes;
    }


    // =========================================================
    // UPDATE BOUNDARY
    // =========================================================

    private void UpdateBoundaryVisual(
        PhotoFrameResponse response)
    {
        if (response == null)
            return;

        bool good =
            response.face_found &&
            response.is_forward &&
            response.is_centered;

        if (boundaryOutline == null)
            return;

        Color targetColor =
            good
                ? detectedColor
                : undetectedColor;

        foreach (
            Graphic childGraphic
            in boundaryOutline
                .GetComponentsInChildren<Graphic>()
        )
        {
            childGraphic.color =
                targetColor;
        }
    }


    // =========================================================
    // CAPTURE PHOTO
    // =========================================================

    private IEnumerator CapturePhoto(
        byte[] jpegBytes)
    {
        if (!isActiveAndEnabled)
            yield break;

        if (SessionManager.Instance == null)
            yield break;

        if (detectionService == null)
            yield break;

        if (promptText != null)
        {
            promptText.SetActive(false);
        }

        if (retryText != null)
        {
            retryText.SetActive(false);
        }

        WWWForm form =
            new WWWForm();

        form.AddField(
            "session_id",
            SessionManager.Instance.CurrentSessionId
        );

        form.AddBinaryData(
            "frame",
            jpegBytes,
            "frame.jpg",
            "image/jpeg"
        );

        string url =
            $"{detectionService.baseUrl}/session/capture-photo";

        using (
            UnityWebRequest request =
                UnityWebRequest.Post(url, form)
        )
        {
            yield return request.SendWebRequest();

            // Screen changed while request was running.
            if (!isActiveAndEnabled)
            {
                captureInFlight = false;
                yield break;
            }

            // =================================================
            // SUCCESS
            // =================================================

            if (
                request.result ==
                UnityWebRequest.Result.Success
            )
            {
                SessionResponse response =
                    JsonUtility.FromJson<
                        SessionResponse
                    >(
                        request.downloadHandler.text
                    );

                if (response == null)
                {
                    Debug.LogError(
                        "[CapturePhotoScreen] " +
                        "Invalid capture-photo response."
                    );

                    captureInFlight = false;
                    StartPolling();

                    yield break;
                }

                Debug.Log(
                    "[CapturePhotoScreen] " +
                    $"capture-photo success. " +
                    $"State = {response.state}"
                );

                if (face != null)
                {
                    face.SetExpression(
                        FaceExpression.Success
                    );
                }

                // Robot finishes speaking before we leave this
                // screen.
                if (SessionManager.Instance != null)
                {
                    yield return
                        SessionManager.Instance
                            .PlayResponseAudio(
                                response
                            );
                }

                if (!isActiveAndEnabled)
                {
                    captureInFlight = false;
                    yield break;
                }

                // =================================================
                // NEXT SCREEN
                // =================================================

                if (flowManager != null)
                {
                    flowManager.GoTo(
                        VisitorFlowState
                            .MeetSomeone_ShowSimilarNames
                    );
                }

                captureInFlight = false;
            }

            // =================================================
            // 409
            // =================================================

            else if (
                request.responseCode == 409
            )
            {
                Debug.LogWarning(
                    "[CapturePhotoScreen] " +
                    "capture-photo rejected (409): " +
                    request.downloadHandler.text
                );

                if (retryText != null)
                {
                    retryText.SetActive(true);
                }

                if (promptText != null)
                {
                    promptText.SetActive(true);
                }

                captureInFlight = false;

                StartPolling();
            }

            // =================================================
            // OTHER ERROR
            // =================================================

            else
            {
                Debug.LogWarning(
                    "[CapturePhotoScreen] " +
                    "capture-photo failed: " +
                    request.error
                );

                captureInFlight = false;

                StartPolling();
            }
        }
    }


    // =========================================================
    // RESET
    // =========================================================

    public void ResetScreen()
    {
        captureInFlight = false;

        if (cameraPreview != null)
        {
            cameraPreview.gameObject.SetActive(true);
        }

        if (boundaryOutline != null)
        {
            boundaryOutline.SetActive(true);

            foreach (
                Graphic childGraphic
                in boundaryOutline
                    .GetComponentsInChildren<Graphic>()
            )
            {
                childGraphic.color =
                    undetectedColor;
            }
        }

        if (promptText != null)
        {
            promptText.SetActive(true);
        }

        if (retryText != null)
        {
            retryText.SetActive(false);
        }
    }
}

