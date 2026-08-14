using UnityEngine;
using TMPro;

public class ConversationScreen : MonoBehaviour
{
    [Header("UI Text & Indicators")]
    public TextMeshProUGUI heardText;
    public CaptionBarController captionBar;

    [Header("Flow")]
    public VisitorFlowManager flowManager;
    public FaceExpressionController face;

    [Header("Silence / Abandonment Handling")]
    [Tooltip("How many times we retry listening after silence before giving up and going back to idle.")]
    public int maxSilenceRetries = 1;

    private bool isStartingRecording = false;
    private int silenceRetryCount = 0;

    // =========================================================
    // ENABLE
    // =========================================================

    void OnEnable()
    {
        Debug.Log(
            "[ConversationScreen] ENABLED"
        );

        isStartingRecording = false;
        silenceRetryCount = 0;

        if (SessionManager.Instance != null)
        {
            SessionManager.Instance.OnSessionUpdate +=
                HandleSessionUpdate;

            SessionManager.Instance.OnRecordingFailed +=
                HandleRecordingFailed;

            SessionManager.Instance.OnReadyToSpeak +=
                HandleReadyToSpeak;

            SessionManager.Instance.OnRobotSpeaking +=
                HandleRobotSpeakingForIndicator;
        }

        // Do NOT check IsResponseAudioActive.
        // Your SessionManager does not have that property.
        //
        // SessionManager already sends OnSessionUpdate only
        // AFTER response audio has finished.
        StartListening();
    }

    // =========================================================
    // DISABLE
    // =========================================================

    void OnDisable()
    {
        Debug.Log(
            "[ConversationScreen] DISABLED"
        );

        if (SessionManager.Instance != null)
        {
            SessionManager.Instance.CancelPendingRecording();

            SessionManager.Instance.OnSessionUpdate -=
                HandleSessionUpdate;

            SessionManager.Instance.OnRecordingFailed -=
                HandleRecordingFailed;

            SessionManager.Instance.OnReadyToSpeak -=
                HandleReadyToSpeak;

            SessionManager.Instance.OnRobotSpeaking -=
                HandleRobotSpeakingForIndicator;
        }

        isStartingRecording = false;
        silenceRetryCount = 0;

        if (captionBar != null)
            captionBar.HideListening();
    }

    // =========================================================
    // START LISTENING
    // =========================================================

    private void StartListening()
    {
        if (!isActiveAndEnabled)
            return;

        if (isStartingRecording)
            return;

        if (SessionManager.Instance == null)
            return;

        isStartingRecording = true;

        Debug.Log(
            "[ConversationScreen] " +
            "Starting recording."
        );

        SessionManager.Instance.RecordAndSend();

        isStartingRecording = false;
    }

    // =========================================================
    // READY
    // =========================================================

    private void HandleReadyToSpeak()
    {
        if (!isActiveAndEnabled)
            return;

        if (captionBar != null)
            captionBar.ShowListening();

        Debug.Log(
            "[ConversationScreen] " +
            "READY TO SPEAK"
        );
    }

    // =========================================================
    // SESSION UPDATE
    // =========================================================

    private void HandleSessionUpdate(
        SessionResponse response)
    {
        if (!isActiveAndEnabled ||
            response == null)
        {
            return;
        }

        // Got a real response from the backend — visitor is still there.
        // Reset the abandonment counter.
        silenceRetryCount = 0;

        Debug.Log(
            $"[ConversationScreen] " +
            $"Received state: {response.state}"
        );

        if (captionBar != null)
            captionBar.HideListening();

        if (heardText != null)
        {
            heardText.text =
                string.IsNullOrEmpty(
                    response.heard_text
                )
                ? ""
                : $"You said: {response.heard_text}";
        }

        // =====================================================
        // HOST CANDIDATES
        // =====================================================

        if (response.state == "HOST_SELECTION" ||
            response.state == "HOST_SUGGESTIONS")
        {
            if (flowManager != null &&
                flowManager.Session != null)
            {
                flowManager.Session.hostCandidates =
                    response.host_candidates;

                flowManager.GoTo(
                    VisitorFlowState.HostCandidatesSelection
                );
            }

            return;
        }

        // =====================================================
        // NAME CONFIRMATION
        // =====================================================

        if (response.state == "NAME_CONFIRMATION")
        {
            Debug.Log(
                "[ConversationScreen] " +
                "Opening NameConfirmationScreen."
            );

            if (flowManager != null &&
                flowManager.Session != null)
            {
                flowManager.Session.visitorName =
                    response.heard_text;

                flowManager.GoTo(
                    VisitorFlowState.NameConfirmation
                );
            }

            return;
        }

        // =====================================================
        // STATES THAT REQUIRE VISITOR TO SPEAK
        // =====================================================

        if (response.state == "AWAITING_PURPOSE" ||
            response.state == "AWAITING_NAME" ||
            response.state == "AWAITING_HOST_NAME" ||
            response.state == "AWAITING_INTENT" ||
            response.state == "ANYTHING_ELSE" ||
            response.state == "QUERY_ANSWERED" ||
            response.state == "FALLBACK")
        {
            Debug.Log(
                "[ConversationScreen] " +
                "Backend expects visitor response. " +
                "Starting listening."
            );

            StartListening();

            return;
        }

        // =====================================================
        // HANDOFF
        // =====================================================

        if (response.state == "READY_FOR_HANDOFF")
        {
            if (flowManager != null &&
                flowManager.Session != null)
            {
                if (response.matched_host != null)
                {
                    flowManager.Session.hostName =
                        response.matched_host.name;
                }

                flowManager.Session.qrBase64 =
                    response.qr_base64;

                flowManager.GoTo(
                    VisitorFlowState.MeetSomeone_ShowSimilarNames
                );
            }

            return;
        }

        // =====================================================
        // PHOTO
        // =====================================================

        if (response.state == "AWAITING_PHOTO")
        {
            Debug.Log(
                "[ConversationScreen] " +
                "Backend requested photo."
            );

            if (flowManager != null &&
                flowManager.Session != null)
            {
                if (response.matched_host != null)
                {
                    flowManager.Session.hostName =
                        response.matched_host.name;
                }

                flowManager.GoTo(
                    VisitorFlowState.CapturePhoto
                );
            }

            return;
        }

        // =====================================================
        // UNKNOWN
        // =====================================================

        Debug.LogWarning(
            "[ConversationScreen] " +
            $"Unhandled state: {response.state}"
        );

        if (heardText != null)
        {
            heardText.text =
                "Sorry, I didn't understand that. Please try again.";
        }

        if (face != null)
        {
            face.SetExpression(
                FaceExpression.Confused
            );
        }

        StartListening();
    }

    // =========================================================
    // RECORDING FAILED (SILENCE / NO SPEECH DETECTED)
    // =========================================================

    private void HandleRecordingFailed()
    {
        if (!isActiveAndEnabled)
            return;

        silenceRetryCount++;

        if (silenceRetryCount > maxSilenceRetries)
        {
            Debug.Log(
                "[ConversationScreen] " +
                $"No response after {silenceRetryCount} attempt(s) — " +
                "assuming visitor left. Returning to idle."
            );

            if (SessionManager.Instance != null)
                SessionManager.Instance.EndSession();

            silenceRetryCount = 0;

            if (flowManager != null)
                flowManager.GoTo(VisitorFlowState.Idle);

            return;
        }

        Debug.LogWarning(
            "[ConversationScreen] " +
            $"Recording failed (attempt {silenceRetryCount}/{maxSilenceRetries}) — retrying."
        );

        if (heardText != null)
        {
            heardText.text =
                "Didn't catch that. Please try again.";
        }

        if (face != null)
        {
            face.SetExpression(
                FaceExpression.Confused
            );
        }

        StartListening();
    }

    // =========================================================
    // ROBOT SPEAKING
    // =========================================================

    private void HandleRobotSpeakingForIndicator(
    string text, float duration)
    {   
        if (!isActiveAndEnabled)
            return;

        if (captionBar != null)
            captionBar.HideListening();

        Debug.Log(
        $"[ConversationScreen] Robot speaking: {text}"
        );
    }
}