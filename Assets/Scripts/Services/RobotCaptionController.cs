using UnityEngine;

public class RobotCaptionController : MonoBehaviour
{
    public static RobotCaptionController Instance;

    public CaptionBarController captionBar;

    private bool suppressed = false;
    private string pendingText = null; // remembers what was said while suppressed, in case caller wants it later

    void Awake() { Instance = this; }

    void Start()
    {
        SessionManager.Instance.OnRobotSpeaking += HandleRobotSpeaking;
        SessionManager.Instance.OnRobotFinishedSpeaking += HandleRobotFinishedSpeaking;

        if (captionBar != null)
            captionBar.HideCaption();
    }

    void OnDisable()
    {
        if (SessionManager.Instance != null)
        {
            SessionManager.Instance.OnRobotSpeaking -= HandleRobotSpeaking;
            SessionManager.Instance.OnRobotFinishedSpeaking -= HandleRobotFinishedSpeaking;
        }
    }

    public void SetSuppressed(bool value)
    {
        suppressed = value;
        if (suppressed && captionBar != null)
            captionBar.HideCaption();
    }

    void HandleRobotSpeaking(string text, float duration)
    {
        pendingText = text;

        if (suppressed) return; // Capture Photo (or any screen) can silence captions without touching this script

        if (captionBar != null)
            captionBar.ShowCaption(text, duration);
    }

    void HandleRobotFinishedSpeaking()
    {
        if (captionBar != null)
            captionBar.HideCaption();
    }
}