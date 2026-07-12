using UnityEngine;
using System.Collections;
using TMPro;

public class VisitorWaitingScreen : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI waitMessageText;
    public GameObject waitMorePromptGroup; // parent object holding the prompt + 2 buttons

    [Header("Dependencies")]
    public VisitorFlowManager flowManager;
    public FaceExpressionController face;

    [Header("Timing")]
    public float waitDuration = 15f; // stub — flowchart implies host-specified time, shortened for testing

    private Coroutine waitRoutine;

    void OnEnable()
    {
        if (flowManager == null)
        {
            Debug.LogError("CRITICAL: flowManager field is BLANK in Screen_VisitorWaiting Inspector!");
            return;
        }

        string hostName = flowManager.Session.hostName;
        waitMessageText.text = $"Please wait, {hostName} will be with you shortly.";
        waitMorePromptGroup.SetActive(false);

        waitRoutine = StartCoroutine(WaitThenPrompt());
    }

    void OnDisable()
    {
        if (waitRoutine != null)
            StopCoroutine(waitRoutine);
    }

    IEnumerator WaitThenPrompt()
    {
        yield return new WaitForSeconds(waitDuration);
        waitMessageText.gameObject.SetActive(false);
        // STUB — no real "host available now" check from backend yet.
        // Show the wait-more prompt, per flowchart's "Wait more?" branch.
        waitMorePromptGroup.SetActive(true);
    }

    public void OnYesWaitMore()
    {
        Debug.Log("Visitor chose to keep waiting.");
        waitMorePromptGroup.SetActive(false);
        waitMessageText.gameObject.SetActive(true);
        waitRoutine = StartCoroutine(WaitThenPrompt());
    }

    public void OnNoLeave()
    {
        Debug.Log("Visitor chose not to wait further.");
        flowManager.GoTo(VisitorFlowState.VisitLogged);
    }

    // Called once real backend confirms host is now available —
    // exposed publicly so it's ready to hook up later.
    public void HandleHostNowAvailable()
    {
        if (waitRoutine != null) StopCoroutine(waitRoutine);
        Debug.Log("Host is now available.");

        if (face != null)
            face.SetExpression(FaceExpression.Happy);

        flowManager.GoTo(VisitorFlowState.HostAccepted);
    }
}