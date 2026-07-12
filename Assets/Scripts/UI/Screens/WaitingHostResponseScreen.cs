using UnityEngine;
using System.Collections;

public class WaitingHostResponseScreen : MonoBehaviour
{
    [Header("UI References")]
    public TMPro.TextMeshProUGUI statusText;
    public GameObject waitIndicator;

    [Header("Dependencies")]
    public VisitorFlowManager flowManager;
    public FaceExpressionController face;

    [Header("Timing")]
    public float timeoutSeconds = 15f; // stub — flowchart says 2 min, shortened for testing

    private Coroutine waitRoutine;

    void OnEnable()
    {
        if (flowManager == null)
        {
            Debug.LogError("CRITICAL: flowManager field is BLANK in Screen_WaitingHostResponse Inspector!");
            return;
        }

        string hostName = flowManager.Session.hostName;
        statusText.text = $"Notifying {hostName}...";

        if (face != null)
            face.SetExpression(FaceExpression.Thinking, autoReturnToIdle: false);

        waitRoutine = StartCoroutine(WaitForHostResponse());
    }

    void OnDisable()
    {
        if (waitRoutine != null)
            StopCoroutine(waitRoutine);
    }

    IEnumerator WaitForHostResponse()
    {
        yield return new WaitForSeconds(timeoutSeconds);

        // STUB — no real host-response backend yet.
        // For now, always treat as "no response" to test the retry branch.
        HandleNoResponse();
    }

    private void HandleNoResponse()
    {
        flowManager.Session.alertRetryCount++;
        Debug.Log($"No response from host. Retry count: {flowManager.Session.alertRetryCount}");

        if (flowManager.Session.alertRetryCount < 2)
        {
            statusText.text = "Retrying...";
            waitRoutine = StartCoroutine(WaitForHostResponse());
        }
        else
        {
            Debug.Log("Retry limit reached, host cannot be reached.");
            flowManager.GoTo(VisitorFlowState.HostUnavailable);
        }
    }

    // These three would be called by real backend responses once that exists —
    // exposed as public methods now so they're easy to hook up later.

    public void HandleHostAccepted()
    {
        if (waitRoutine != null) StopCoroutine(waitRoutine);
        Debug.Log("Host accepted the visit.");
        flowManager.GoTo(VisitorFlowState.HostAccepted);
    }

    public void HandleHostUnavailable()
    {
        if (waitRoutine != null) StopCoroutine(waitRoutine);
        Debug.Log("Host marked unavailable.");
        flowManager.GoTo(VisitorFlowState.HostUnavailable);
    }
}