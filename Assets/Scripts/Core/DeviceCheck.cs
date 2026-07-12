using UnityEngine;
using UnityEngine.Android;
using System.Collections;

public class DeviceCheck : MonoBehaviour
{
    public FaceExpressionController face;
    public WebCamTexture camTexture;
    private string micDevice;

    void Start()
    {
        // Camera Permission
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            var cameraCallbacks = new PermissionCallbacks();
            cameraCallbacks.PermissionGranted += OnCameraPermissionGranted;
            cameraCallbacks.PermissionDenied += (permissionName) =>
                Debug.LogWarning("Camera permission denied.");

            Permission.RequestUserPermission(Permission.Camera, cameraCallbacks);
        }
        else
        {
            StartCamera();
        }

        // Microphone Permission
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            var micCallbacks = new PermissionCallbacks();
            micCallbacks.PermissionGranted += OnMicPermissionGranted;
            micCallbacks.PermissionDenied += (permissionName) =>
                Debug.LogWarning("Microphone permission denied.");

            Permission.RequestUserPermission(Permission.Microphone, micCallbacks);
        }
        else
        {
            StartMicrophone();
        }
    }

    private void OnCameraPermissionGranted(string permissionName)
    {
        Debug.Log("Camera permission granted.");
        StartCamera();
    }

    private void OnMicPermissionGranted(string permissionName)
    {
        Debug.Log("Microphone permission granted.");
        StartMicrophone();
    }

    private void StartCamera()
    {
        WebCamDevice[] devices = WebCamTexture.devices;

        if (devices.Length == 0)
        {
            Debug.LogWarning("No camera devices found.");
            return;
        }

        // Log every camera
        for (int i = 0; i < devices.Length; i++)
        {
            Debug.Log($"Camera {i}: {devices[i].name}, FrontFacing: {devices[i].isFrontFacing}");
        }

        // Default to first camera
        string selectedCamera = devices[0].name;

        // Prefer front camera
        foreach (var device in devices)
        {
            if (device.isFrontFacing)
            {
                selectedCamera = device.name;
                break;
            }
        }

        Debug.Log("Using camera: " + selectedCamera);

        camTexture = new WebCamTexture(selectedCamera);
        camTexture.Play();

        Debug.Log("Camera started: " + camTexture.isPlaying);

        FaceDetectionService detectionService = FindAnyObjectByType<FaceDetectionService>();

        if (detectionService != null)
        {
            StartCoroutine(StartDetectionWhenCameraReady(detectionService));
        }
        else
        {
            Debug.LogError("FaceDetectionService not found!");
        }
    }

    private IEnumerator StartDetectionWhenCameraReady(FaceDetectionService detectionService)
    {
        while (!camTexture.isPlaying || camTexture.width <= 16 || camTexture.height <= 16)
        {
            yield return null;
        }

        Debug.Log($"Camera ready: {camTexture.width} x {camTexture.height}");

        detectionService.webcamTexture = camTexture;
        detectionService.StartPolling();
    }

    private void StartMicrophone()
    {
        if (Microphone.devices.Length > 0)
        {
            micDevice = Microphone.devices[0];
            AudioClip clip = Microphone.Start(micDevice, true, 10, 44100);

            Debug.Log("Mic started: " + (clip != null));
        }
        else
        {
            Debug.LogWarning("No microphone devices found.");
        }
    }

    private IEnumerator ReturnToIdleAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);

        if (face != null)
            face.ReturnToIdle();
    }

    private void OnDisable()
    {
        if (camTexture != null && camTexture.isPlaying)
            camTexture.Stop();

        if (!string.IsNullOrEmpty(micDevice) && Microphone.IsRecording(micDevice))
            Microphone.End(micDevice);
    }

    private void OnDestroy()
    {
        OnDisable();
    }
}