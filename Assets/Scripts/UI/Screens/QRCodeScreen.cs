using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class QRCodeScreen : MonoBehaviour
{
    public RawImage qrImage;
    public TextMeshProUGUI messageText;
    public VisitorFlowManager flowManager;
    public FaceExpressionController face;
    public Texture2D staticQRFallback;

    [Header("Timing")]
    public float displayDuration = 8f;

    void OnEnable()
    {
        string hostName = flowManager.Session.hostName;

        string spokenMessage = $"I am notifying {hostName}. Please scan this code — further updates will be sent to your phone.";
        messageText.text = spokenMessage;

        if (AndroidTTS.Instance != null)
            AndroidTTS.Instance.Speak(spokenMessage);

        if (face != null)
            face.SetExpression(FaceExpression.Success);

        string qrBase64 = flowManager.Session.qrBase64;

        if (!string.IsNullOrEmpty(qrBase64))
        {
            Texture2D tex = DecodeBase64ToTexture(qrBase64);
            qrImage.texture = tex != null ? tex : staticQRFallback;
        }
        else
        {
            Debug.LogWarning("No qr_base64 in session response — using static fallback.");
            qrImage.texture = staticQRFallback;
        }

        StartCoroutine(ReturnToIdleAfterDelay());
    }

    private Texture2D DecodeBase64ToTexture(string base64)
    {
        try
        {
            byte[] bytes = System.Convert.FromBase64String(base64);
            Texture2D tex = new Texture2D(2, 2); // size gets replaced by LoadImage
            if (tex.LoadImage(bytes))
                return tex;

            Debug.LogWarning("QR base64 decoded but LoadImage failed.");
            return null;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Failed to decode QR base64: " + e.Message);
            return null;
        }
    }

    IEnumerator ReturnToIdleAfterDelay()
    {
        yield return new WaitForSeconds(displayDuration);
        flowManager.GoTo(VisitorFlowState.Idle); // this deactivates this GameObject — do nothing after this line
    }
}