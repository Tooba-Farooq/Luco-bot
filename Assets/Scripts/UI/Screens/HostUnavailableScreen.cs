using UnityEngine;
using TMPro;

public class HostUnavailableScreen : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI messageText;

    [Header("Dependencies")]
    public VisitorFlowManager flowManager;
    public FaceExpressionController face;

    void OnEnable()
    {
        if (flowManager == null)
        {
            Debug.LogError("CRITICAL: flowManager field is BLANK in Screen_HostUnavailable Inspector!");
            return;
        }

        string hostName = flowManager.Session.hostName;
        messageText.text = $"Sorry, {hostName} is currently unavailable.";

        if (face != null)
            face.SetExpression(FaceExpression.Apologetic);
    }

    public void OnWaitSelected()
    {
        Debug.Log("Visitor chose to wait.");
        flowManager.GoTo(VisitorFlowState.VisitorWaiting);
    }

    public void OnMessageSelected()
    {
        Debug.Log("Visitor chose to leave a message.");
        flowManager.GoTo(VisitorFlowState.RecordMessage);
    }

    public void OnCancelSelected()
    {
        Debug.Log("Visitor cancelled the visit.");
        flowManager.GoTo(VisitorFlowState.Idle);
    }
}