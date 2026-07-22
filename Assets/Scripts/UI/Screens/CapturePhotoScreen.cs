using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CapturePhotoScreen : MonoBehaviour
{
    [Header("UI References")]
    public RawImage cameraPreview;
    public RawImage capturedPreview;
    public GameObject captureButton; // Keep this just in case, or you can hide it!
    public GameObject retakeButton;
    public GameObject confirmButton;
    public GameObject promptText;

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

    private Texture2D capturedTexture;
    private bool isFaceDetected = false;
    private Coroutine autocaptureCoroutine = null; // Track our autocapture timer

    void OnEnable()
    {
        if (detectionService != null)
        {
            detectionService.OnDetectionResult += HandleDetectionUpdate;
            detectionService.StartPolling(); 
        }
        else
        {
            Debug.LogError("CRITICAL: FaceDetectionService is missing from CapturePhotoScreen!", this);
        }
        
        if (face != null && lookAtCameraClip != null)
        {
            face.StartTalking(lookAtCameraClip);
        }

        ResetScreen();
    }

    void OnDisable()
    {
        if (detectionService != null)
        {
            detectionService.OnDetectionResult -= HandleDetectionUpdate;
            detectionService.StopPolling(); 
        }
        StopAutocapture();
    }

    void Update()
    {
        if (cameraPreview.gameObject.activeSelf && deviceCheck.camTexture != null)
        {
            cameraPreview.texture = deviceCheck.camTexture;
        }
    }

    private void HandleDetectionUpdate(DetectResponse result) 
    {
        if (result == null || !cameraPreview.gameObject.activeSelf) return;

        isFaceDetected = result.face_forward;

        // Apply colors to outline
        if (boundaryOutline != null)
        {
            Color targetColor = isFaceDetected ? detectedColor : undetectedColor;
            foreach (Graphic childGraphic in boundaryOutline.GetComponentsInChildren<Graphic>())
            {
                childGraphic.color = targetColor;
            }
        }

        // Manage the Autocapture state machine
        if (isFaceDetected)
        {
            // If they just looked forward and we aren't already counting down, start!
            if (autocaptureCoroutine == null)
            {
                autocaptureCoroutine = StartCoroutine(AutocaptureTimer(1.0f)); // 1.0 second delay
            }
        }
        else
        {
            // If they look away before the capture, cancel the countdown immediately
            StopAutocapture();
        }

        // We can keep the manual capture button active just as a backup
        if (captureButton != null)
        {
            captureButton.SetActive(isFaceDetected);
        }
    }

    private IEnumerator AutocaptureTimer(float delay)
    {
        // Wait for the face to stay aligned for the duration of the delay
        yield return new WaitForSeconds(delay);

        // Snap the photo!
        Debug.Log("Autocapture triggered!");
        OnCapture();
        autocaptureCoroutine = null;
    }

    private void StopAutocapture()
    {
        if (autocaptureCoroutine != null)
        {
            StopCoroutine(autocaptureCoroutine);
            autocaptureCoroutine = null;
            Debug.Log("Autocapture cancelled (visitor looked away).");
        }
    }

    public void OnCapture()
    {
        // Prevent autocapture from firing again if they manually pressed it
        StopAutocapture();

        if (deviceCheck.camTexture == null)
        {
            Debug.LogWarning("No camera texture available to capture.");
            return;
        }

        WebCamTexture cam = deviceCheck.camTexture;
        capturedTexture = new Texture2D(cam.width, cam.height, TextureFormat.RGB24, false);
        capturedTexture.SetPixels(cam.GetPixels());
        capturedTexture.Apply();

        capturedPreview.texture = capturedTexture;
        cameraPreview.gameObject.SetActive(false);
        capturedPreview.gameObject.SetActive(true);
        
        if (boundaryOutline != null) 
            boundaryOutline.SetActive(false); 

        captureButton.SetActive(false);
        retakeButton.SetActive(true);
        confirmButton.SetActive(true);
        promptText.SetActive(false);
    }

    public void OnRetake()
    {
        if (capturedTexture != null)
            Destroy(capturedTexture);

        ResetScreen();
    }

    public void OnConfirm()
    {
        if (flowManager.Session != null)
            flowManager.Session.visitorPhoto = capturedTexture;

        if (face != null)
            face.SetExpression(FaceExpression.Success);

        Debug.Log("Photo confirmed, moving to QR code");
        flowManager.GoTo(VisitorFlowState.MeetSomeone_ShowSimilarNames); 
    }

    public void ResetScreen()
    {
        StopAutocapture();
        isFaceDetected = false;
        cameraPreview.gameObject.SetActive(true);
        capturedPreview.gameObject.SetActive(false);
        
        if (boundaryOutline != null)
        {
            boundaryOutline.SetActive(true);
            foreach (Graphic childGraphic in boundaryOutline.GetComponentsInChildren<Graphic>())
            {
                childGraphic.color = undetectedColor;
            }
        }

        if (captureButton != null)
            captureButton.SetActive(false); 
        
        retakeButton.SetActive(false);
        confirmButton.SetActive(false);
        promptText.SetActive(true);
    }
}