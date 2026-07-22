using UnityEngine;
using TMPro;

public class ConfirmSpellingScreen : MonoBehaviour
{
    public TextMeshProUGUI spellingText;
    public VisitorFlowManager flowManager;
    public FaceExpressionController face;

    void OnEnable()
    {
        string name = flowManager.Session.visitorName;

        spellingText.text = $"Did you say {name}?";

        if (AndroidTTS.Instance != null)
            AndroidTTS.Instance.Speak(name + "?"); // full name, question tone via trailing "?"
    }

    public void OnYes()
    {
        Debug.Log("Name confirmed: " + flowManager.Session.visitorName);
        flowManager.GoTo(VisitorFlowState.CapturePhoto);
    }

    public void OnNo()
    {
        Debug.Log("Name rejected, going to correction screen.");
        flowManager.GoTo(VisitorFlowState.MeetSomeone_EnterPurpose);
    }
}