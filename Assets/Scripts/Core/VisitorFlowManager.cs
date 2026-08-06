using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class VisitorFlowManager : MonoBehaviour
{
    public VisitorFlowState CurrentState { get; private set; }
        = VisitorFlowState.Idle;

    public VisitorSession Session { get; private set; }
        = new VisitorSession();

    [Header("Face")]
    public FaceExpressionController face;

    [Header("UI")]
    public UIManager uiManager;

    [Header("Detection Resume")]
    public FaceDetectionService detectionService;
    public VisitorDetectionHandler detectionHandler;

    public float postQRCooldown = 4f;

    // =========================================================
    // START
    // =========================================================

    void Start()
    {
        GoTo(VisitorFlowState.Idle);
    }

    // =========================================================
    // FLOW TRANSITION
    // =========================================================

    public void GoTo(VisitorFlowState next)
    {
        VisitorFlowState previous =
            CurrentState;

        Debug.Log(
            $"[FLOW] {previous} -> {next}"
        );

        CurrentState = next;

        // UI is controlled centrally.
        //
        // UIManager hides every other screen before
        // activating the new one.
        if (uiManager != null)
        {
            uiManager.ShowScreen(next);
        }

        switch (next)
        {
            case VisitorFlowState.Idle:

                Session.Reset();

                if (face != null)
                    face.ReturnToIdle();

                if (previous ==
                    VisitorFlowState.MeetSomeone_ShowSimilarNames)
                {
                    StartCoroutine(
                        ResumeDetectionAfterCooldown()
                    );
                }

                break;

            case VisitorFlowState.FaceRecognition:

            case VisitorFlowState.MeetSomeone_HostLookup:

                if (face != null)
                {
                    face.SetExpression(
                        FaceExpression.Thinking,
                        autoReturnToIdle: false
                    );
                }

                break;

            case VisitorFlowState.AlertingHost:

                if (face != null)
                {
                    face.SetExpression(
                        FaceExpression.Thinking,
                        autoReturnToIdle: false
                    );
                }

                break;

            case VisitorFlowState.CollectName:

            case VisitorFlowState.MeetSomeone_EnterHostName:

            case VisitorFlowState.GeneralQuery:

                if (face != null)
                {
                    face.SetExpression(
                        FaceExpression.Listening,
                        autoReturnToIdle: false
                    );
                }

                break;

            case VisitorFlowState.AskPurpose:

            case VisitorFlowState.MeetSomeone_EnterPurpose:

                if (face != null)
                {
                    face.SetExpression(
                        FaceExpression.Purpose
                    );
                }

                break;

            case VisitorFlowState.NameConfirmation:

                if (face != null)
                {
                    face.SetExpression(
                        FaceExpression.Listening,
                        autoReturnToIdle: false
                    );
                }

                break;

            case VisitorFlowState.GreetKnownVisitor:

                if (face != null)
                {
                    face.SetExpression(
                        FaceExpression.Greeting
                    );
                }

                break;

            case VisitorFlowState.HostCandidatesSelection:

                if (face != null)
                {
                    face.SetExpression(
                        FaceExpression.Listening,
                        autoReturnToIdle: false
                    );
                }

                break;

            case VisitorFlowState.HostAccepted:

                if (face != null)
                {
                    face.SetExpression(
                        FaceExpression.Happy
                    );
                }

                break;

            case VisitorFlowState.HostUnavailable:

                if (face != null)
                {
                    face.SetExpression(
                        FaceExpression.Apologetic
                    );
                }

                break;

            case VisitorFlowState.MeetSomeone_ShowSimilarNames:

                if (face != null)
                {
                    face.SetExpression(
                        FaceExpression.Handoff
                    );
                }

                break;

            case VisitorFlowState.VisitLogged:

                if (face != null)
                {
                    face.SetExpression(
                        FaceExpression.Success
                    );
                }

                break;
        }
    }

    // =========================================================
    // RESUME DETECTION
    // =========================================================

    private IEnumerator ResumeDetectionAfterCooldown()
    {
        yield return new WaitForSeconds(
            postQRCooldown
        );

        if (detectionHandler != null)
        {
            detectionHandler.ResetDetectionState();
        }

        if (detectionService != null)
        {
            detectionService.StartPolling();
        }
    }

#if UNITY_EDITOR

    void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current.hKey.wasPressedThisFrame)
        {
            Session.hostCandidates =
                new HostCandidate[]
                {
                    new HostCandidate
                    {
                        id = 1,
                        name = "Ahmed Khan"
                    },

                    new HostCandidate
                    {
                        id = 2,
                        name = "Sara Ali"
                    }
                };

            GoTo(
                VisitorFlowState.HostCandidatesSelection
            );
        }
    }

#endif
}

