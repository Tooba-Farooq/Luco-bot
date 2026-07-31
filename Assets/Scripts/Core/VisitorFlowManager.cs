using UnityEngine;
using System;
using System.Collections;

public class VisitorFlowManager : MonoBehaviour
{
    public VisitorFlowState CurrentState { get; private set; } = VisitorFlowState.Idle;
    public VisitorSession Session { get; private set; } = new VisitorSession();

    [Header("Face")]
    public FaceExpressionController face;

    [Header("UI")]
    public UIManager uiManager;

    [Header("Detection Resume")]
    public FaceDetectionService detectionService;
    public VisitorDetectionHandler detectionHandler;
    public float postQRCooldown = 4f;

    void Start()
    {
        GoTo(VisitorFlowState.Idle);
    }

    public void GoTo(VisitorFlowState next)
    {
        VisitorFlowState previous = CurrentState;
        CurrentState = next;

        if (uiManager != null)
            uiManager.ShowScreen(next);

        switch (next)
        {
            case VisitorFlowState.Idle:
                Session.Reset();
                if (face != null) face.ReturnToIdle();

                if (previous == VisitorFlowState.MeetSomeone_ShowSimilarNames)
                    StartCoroutine(ResumeDetectionAfterCooldown());
                break;

            case VisitorFlowState.FaceRecognition:
            case VisitorFlowState.MeetSomeone_HostLookup:
                if (face != null) face.SetExpression(FaceExpression.Thinking, autoReturnToIdle: false);
                break;

            case VisitorFlowState.AlertingHost:
                if (face != null) face.SetExpression(FaceExpression.Thinking, autoReturnToIdle: false);
                break;

            case VisitorFlowState.CollectName:
            case VisitorFlowState.MeetSomeone_EnterHostName:
            case VisitorFlowState.GeneralQuery:
                if (face != null) face.SetExpression(FaceExpression.Listening, autoReturnToIdle: false);
                break;

            case VisitorFlowState.GreetKnownVisitor:
            case VisitorFlowState.HostAccepted:
                if (face != null) face.SetExpression(FaceExpression.Happy);
                break;

            case VisitorFlowState.HostUnavailable:
                if (face != null) face.SetExpression(FaceExpression.Apologetic);
                break;

            case VisitorFlowState.VisitLogged:
                if (face != null) face.SetExpression(FaceExpression.Success);
                break;
        }
    }

    private IEnumerator ResumeDetectionAfterCooldown()
    {
        yield return new WaitForSeconds(postQRCooldown);

        if (detectionHandler != null)
            detectionHandler.ResetDetectionState();

        if (detectionService != null)
            detectionService.StartPolling();
    }
}