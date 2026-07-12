using UnityEngine;
using UnityEngine.UI;

public class CapturePhotoScreen : MonoBehaviour
{
    [Header("UI References")]
    public RawImage cameraPreview;
    public RawImage capturedPreview;
    public GameObject captureButton;
    public GameObject retakeButton;
    public GameObject confirmButton;
    public GameObject promptText;

    [Header("Dependencies")]
    public DeviceCheck deviceCheck;
    public VisitorFlowManager flowManager;
    public FaceExpressionController face;

    private Texture2D capturedTexture;

    void OnEnable()
    {
        ResetScreen();
    }

    void Update()
    {
        // Keep the live preview updated while camera texture exists
        if (cameraPreview.gameObject.activeSelf && deviceCheck.camTexture != null)
        {
            cameraPreview.texture = deviceCheck.camTexture;
        }
    }

    public void OnCapture()
    {
        if (deviceCheck.camTexture == null)
        {
            Debug.LogWarning("No camera texture available to capture.");
            return;
        }

        // Snapshot the current frame into a Texture2D
        WebCamTexture cam = deviceCheck.camTexture;
        capturedTexture = new Texture2D(cam.width, cam.height, TextureFormat.RGB24, false);
        capturedTexture.SetPixels(cam.GetPixels());
        capturedTexture.Apply();

        // Show the captured still, hide the live feed
        capturedPreview.texture = capturedTexture;
        cameraPreview.gameObject.SetActive(false);
        capturedPreview.gameObject.SetActive(true);
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

        Debug.Log("Photo confirmed, moving to AskPurpose");
        flowManager.GoTo(VisitorFlowState.AskPurpose);
    }

    public void ResetScreen()
    {
        cameraPreview.gameObject.SetActive(true);
        capturedPreview.gameObject.SetActive(false);
        captureButton.SetActive(true);
        retakeButton.SetActive(false);
        confirmButton.SetActive(false);
        promptText.SetActive(true);
    }
}