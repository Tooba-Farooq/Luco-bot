using UnityEngine;
using TMPro;

public class RobotCaptionController : MonoBehaviour
{
    public static RobotCaptionController Instance; // ADD

    public GameObject robotCaptionBubble;
    public TextMeshProUGUI robotCaptionText;

    private bool suppressed = false; // ADD
    private string pendingText = null; // ADD — remembers what was said while suppressed, in case caller wants it later

    void Awake() { Instance = this; } // ADD

    void Start()
    {
        SessionManager.Instance.OnRobotSpeaking += HandleRobotSpeaking;
        SessionManager.Instance.OnRobotFinishedSpeaking += HandleRobotFinishedSpeaking;
        robotCaptionBubble.SetActive(false);
    }

    void OnDisable()
    {
        if (SessionManager.Instance != null)
        {
            SessionManager.Instance.OnRobotSpeaking -= HandleRobotSpeaking;
            SessionManager.Instance.OnRobotFinishedSpeaking -= HandleRobotFinishedSpeaking;
        }
    }

    public void SetSuppressed(bool value) // ADD
    {
        suppressed = value;
        if (suppressed)
            robotCaptionBubble.SetActive(false);
    }

    void HandleRobotSpeaking(string text)
    {
        if (suppressed) return; // ADD — Capture Photo (or any screen) can silence captions without touching this script
        robotCaptionText.text = text;
        robotCaptionBubble.SetActive(true);
    }

    void HandleRobotFinishedSpeaking()
    {
        robotCaptionBubble.SetActive(false);
    }
}