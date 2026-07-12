using UnityEngine;
using System.Collections;
using TMPro;

public class VisitLoggedScreen : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI thankYouText;

    [Header("Dependencies")]
    public VisitorFlowManager flowManager;
    public FaceExpressionController face;

    [Header("Timing")]
    public float displayDuration = 4f;

    void OnEnable()
    {
        if (flowManager == null)
        {
            Debug.LogError("CRITICAL: flowManager field is BLANK in Screen_VisitLogged Inspector!");
            return;
        }

        string visitorName = flowManager.Session.visitorName;
        string displayName = string.IsNullOrEmpty(visitorName) ? "there" : visitorName;
        thankYouText.text = $"Thank you, {displayName}! Have a great day.";

        if (face != null)
            face.SetExpression(FaceExpression.Success);

        Debug.Log("Visit logged for: " + displayName);

        StartCoroutine(ReturnToIdleAfterDelay());
    }

    IEnumerator ReturnToIdleAfterDelay()
    {
        yield return new WaitForSeconds(displayDuration);
        flowManager.GoTo(VisitorFlowState.Idle);
    }
}