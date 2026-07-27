using UnityEngine;
using UnityEngine.Networking;
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
        string visitorName = flowManager.Session.visitorName;

        string spokenMessage = $"I am notifying {hostName}. Please scan this code — further updates will be sent to your phone.";
        messageText.text = spokenMessage;

        if (AndroidTTS.Instance != null)
            AndroidTTS.Instance.Speak(spokenMessage);

        if (face != null)
            face.SetExpression(FaceExpression.Success);

        string dummyData = System.Uri.EscapeDataString($"Visit-{visitorName}-{System.DateTime.Now.Ticks}");
        string url = $"https://api.qrserver.com/v1/create-qr-code/?size=400x400&data={dummyData}";
        qrImage.texture = staticQRFallback;

        StartCoroutine(ReturnToIdleAfterDelay());
    }

    IEnumerator FetchQRCode(string url)
    {
        using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(url))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                Texture2D tex = DownloadHandlerTexture.GetContent(www);
                qrImage.texture = tex;
            }
            else
            {
                Debug.LogWarning("QR fetch failed: " + www.error);
            }
        }
    }

    IEnumerator ReturnToIdleAfterDelay()
    {
        yield return new WaitForSeconds(displayDuration);
        flowManager.GoTo(VisitorFlowState.Idle); // this deactivates this GameObject — do nothing after this line
    }
}