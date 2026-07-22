using UnityEngine;
using TMPro;

public class ConfirmHostNameScreen : MonoBehaviour
{
    public TextMeshProUGUI confirmText;
    public VisitorFlowManager flowManager;
    public FaceExpressionController face;

    [Header("Testing / Fallback Options")]
    [Tooltip("If true, overrides the real backend check to force a known visitor flow.")]
    public bool forceSimulateKnown = false; 

    void OnEnable()
    {
        string hostName = flowManager.Session.hostName;
        confirmText.text = $"Did you say {hostName}?";

        if (AndroidTTS.Instance != null)
            AndroidTTS.Instance.Speak(hostName + "?");
    }

    public void OnConfirm()
    {
        Debug.Log("Host name confirmed: " + flowManager.Session.hostName);

        // 1. Determine if this is a known visitor (either live backend or testing override)
        bool isKnown = (flowManager.Session != null && flowManager.Session.isKnownVisitor) || forceSimulateKnown;

        // 2. Branch the flow based on the result
        if (isKnown)
        {
            Debug.Log("Routing KNOWN visitor directly to QR Screen.");
            flowManager.GoTo(VisitorFlowState.MeetSomeone_ShowSimilarNames); // Jumps straight to QR
        }
        else
        {
            Debug.Log("Routing UNKNOWN visitor to Visitor Name Collection.");
            flowManager.GoTo(VisitorFlowState.CollectName); // Jumps to AskVisitorName -> Capture Photo
        }
    }

    public void OnRetry()
    {
        Debug.Log("Host name rejected, returning to entry.");
        flowManager.GoTo(VisitorFlowState.MeetSomeone_EnterHostName); // Back to AskHostName
    }
}