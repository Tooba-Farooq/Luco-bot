using UnityEngine;

public class AskPurposeScreen : MonoBehaviour
{
    [Header("Dependencies")]
    public VisitorFlowManager flowManager;
    public FaceExpressionController face;

    public void OnMeetSomeoneSelected()
    {
        if (flowManager == null)
        {
            Debug.LogError("CRITICAL: flowManager field is BLANK in Screen_AskPurpose Inspector!");
            return;
        }

        Debug.Log("Visitor intent: Meet Someone");
        flowManager.GoTo(VisitorFlowState.MeetSomeone_EnterHostName);
    }

    public void OnGeneralQuerySelected()
    {
        if (flowManager == null)
        {
            Debug.LogError("CRITICAL: flowManager field is BLANK in Screen_AskPurpose Inspector!");
            return;
        }

        Debug.Log("Visitor intent: General Query");

        if (face != null)
            face.SetExpression(FaceExpression.Listening, autoReturnToIdle: false);

        flowManager.GoTo(VisitorFlowState.GeneralQuery);
    }
}