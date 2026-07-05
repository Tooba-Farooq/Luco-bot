using UnityEngine;
using UnityEngine.Android;
using System.Collections;

public class DeviceCheck : MonoBehaviour
{
    public FaceExpressionController face;
    private WebCamTexture camTexture;
    private string micDevice;

    void Start()
    {
        //Camera Permission
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            var cameraCallbacks = new PermissionCallbacks();
            cameraCallbacks.PermissionGranted += OnCameraPermissionGranted;
            cameraCallbacks.PermissionDenied += (permissionName) => Debug.LogWarning("Camera permission denied.");

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
            micCallbacks.PermissionDenied += (permissionName) => Debug.LogWarning("Microphone permission denied.");
            Permission.RequestUserPermission(Permission.Microphone, micCallbacks);
        }
        else
        {
            StartMicrophone();
        }
    }

    private void OnCameraPermissionGranted(string permissionName)
    {
        Debug.Log("Camera permission granted by user callback.");
        StartCamera();
    }

    private void OnMicPermissionGranted(string permissionName)
    {
        Debug.Log("Microphone permission granted by user callback.");
        StartMicrophone();
    }

    private void StartCamera()
    {
        if (WebCamTexture.devices.Length > 0)
        {
            camTexture = new WebCamTexture(WebCamTexture.devices[0].name);
            camTexture.Play();
            Debug.Log("Camera started: " + camTexture.isPlaying);

            if (face != null)
                face.SetExpression(FaceExpression.Happy);
        }
        else
        {
            Debug.LogWarning("No camera devices found physically on the device.");
        }
    }

    private void StartMicrophone()
    {
        if (Microphone.devices.Length > 0)
        {
            micDevice = Microphone.devices[0];
            AudioClip clip = Microphone.Start(micDevice, true, 10, 44100);
            Debug.Log("Mic started: " + (clip != null));

            if (face != null)
            {
                face.SetExpression(FaceExpression.Listening, autoReturnToIdle: false);
                StartCoroutine(ReturnToIdleAfterDelay(5f)); // TEST ONLY — remove once real mic-stop logic exists
            }
        }
        else
        {
            Debug.LogWarning("No microphone devices found physically on the device.");
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