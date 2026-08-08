using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HostCandidatesScreen : MonoBehaviour
{
[Header("Host Selection UI")]
public Transform hostCandidatesContainer;
public GameObject hostCandidateButtonPrefab;
public Button confirmHostButton;
public Button cancelHostButton;


[Header("Visual Root")]
[Tooltip("Optional. Assign the visual content of this screen, NOT the GameObject containing this script.")]
public GameObject visualRoot;

[Header("Face Cover")]
[Tooltip("Image that blocks the robot's face while this screen is active. Toggled alongside visualRoot.")]
public GameObject faceCoverImage;

[Header("Flow")]
public VisitorFlowManager flowManager;
public FaceExpressionController face;

private int selectedCandidateId = -1;
private GameObject selectedCandidateButtonObj = null;

private readonly Color selectedColor = new Color(0.75f, 0.85f, 1f);
private Color defaultCandidateColor = Color.white;

private bool waitingForHostResponse = false;

// =========================================================
// ENABLE
// =========================================================

void OnEnable()
{
    Debug.Log("[HostCandidatesScreen] ENABLED");

    if (SessionManager.Instance != null)
    {
        SessionManager.Instance.OnSessionUpdate += HandleSessionUpdate;
        SessionManager.Instance.OnRecordingFailed += HandleRecordingFailed;
    }

    if (confirmHostButton != null)
        confirmHostButton.onClick.AddListener(OnConfirmHost);

    if (cancelHostButton != null)
        cancelHostButton.onClick.AddListener(OnCancelHost);

    waitingForHostResponse = false;

    ResetScreen();

    if (flowManager != null &&
        flowManager.Session != null)
    {
        ShowHostCandidates(
            flowManager.Session.hostCandidates
        );
    }

    ShowVisuals();
}

// =========================================================
// DISABLE
// =========================================================

void OnDisable()
{
    if (SessionManager.Instance != null)
    {
        SessionManager.Instance.OnSessionUpdate -= HandleSessionUpdate;
        SessionManager.Instance.OnRecordingFailed -= HandleRecordingFailed;
    }

    if (confirmHostButton != null)
        confirmHostButton.onClick.RemoveListener(OnConfirmHost);

    if (cancelHostButton != null)
        cancelHostButton.onClick.RemoveListener(OnCancelHost);

    waitingForHostResponse = false;

    ClearHostCandidatesContainer();
}

// =========================================================
// RESET
// =========================================================

private void ResetScreen()
{
    ClearHostCandidatesContainer();

    selectedCandidateId = -1;
    selectedCandidateButtonObj = null;
    waitingForHostResponse = false;

    SetConfirmCancelInteractable(false);
}

// =========================================================
// VISUALS
// =========================================================

private void HideVisuals()
{
    if (visualRoot != null)
    {
        visualRoot.SetActive(false);
    }
    else
    {
        // Fallback: hide the individual UI elements.
        // Do NOT disable this GameObject because it must continue
        // receiving SessionManager.OnSessionUpdate.
        if (hostCandidatesContainer != null)
            hostCandidatesContainer.gameObject.SetActive(false);

        if (confirmHostButton != null)
            confirmHostButton.gameObject.SetActive(false);

        if (cancelHostButton != null)
            cancelHostButton.gameObject.SetActive(false);
    }

    if (faceCoverImage != null)
        faceCoverImage.SetActive(false);

    Debug.Log(
        "[HostCandidatesScreen] Visuals hidden while waiting for response."
    );
}

private void ShowVisuals()
{
    if (visualRoot != null)
    {
        visualRoot.SetActive(true);
    }
    else
    {
        if (hostCandidatesContainer != null)
            hostCandidatesContainer.gameObject.SetActive(true);

        if (confirmHostButton != null)
            confirmHostButton.gameObject.SetActive(true);

        if (cancelHostButton != null)
            cancelHostButton.gameObject.SetActive(true);
    }

    if (faceCoverImage != null)
        faceCoverImage.SetActive(true);
}

// =========================================================
// BUTTON STATE
// =========================================================

private void SetConfirmCancelInteractable(bool interactable)
{
    if (confirmHostButton != null)
        confirmHostButton.interactable = interactable;

    if (cancelHostButton != null)
        cancelHostButton.interactable = interactable;
}

// =========================================================
// SHOW CANDIDATES
// =========================================================

private void ShowHostCandidates(
    HostCandidate[] candidates)
{
    if (candidates == null ||
        candidates.Length == 0)
    {
        Debug.LogWarning(
            "[HostCandidatesScreen] No host candidates."
        );

        if (flowManager != null)
        {
            flowManager.GoTo(
                VisitorFlowState.MeetSomeone_EnterHostName
            );
        }

        return;
    }

    ClearHostCandidatesContainer();

    foreach (HostCandidate candidate in candidates)
    {
        GameObject btnObj =
            Instantiate(
                hostCandidateButtonPrefab,
                hostCandidatesContainer
            );

        TextMeshProUGUI textComp =
            btnObj.GetComponentInChildren<TextMeshProUGUI>();

        if (textComp != null)
            textComp.text = candidate.name;

        int id = candidate.id;

        Button btn =
            btnObj.GetComponent<Button>();

        if (btn != null)
        {
            btn.onClick.AddListener(
                () => OnCandidateSelected(
                    id,
                    btnObj
                )
            );
        }
    }
}

// =========================================================
// CLEAR
// =========================================================

private void ClearHostCandidatesContainer()
{
    if (hostCandidatesContainer == null)
        return;

    for (int i =
         hostCandidatesContainer.childCount - 1;
         i >= 0;
         i--)
    {
        Destroy(
            hostCandidatesContainer
                .GetChild(i)
                .gameObject
        );
    }
}

// =========================================================
// SELECT CANDIDATE
// =========================================================

private void OnCandidateSelected(
    int employeeId,
    GameObject btnObj)
{
    if (waitingForHostResponse)
        return;

    if (selectedCandidateButtonObj != null)
    {
        Image previousImage =
            selectedCandidateButtonObj
                .GetComponent<Image>();

        if (previousImage != null)
            previousImage.color =
                defaultCandidateColor;
    }

    selectedCandidateId = employeeId;
    selectedCandidateButtonObj = btnObj;

    Image image =
        btnObj.GetComponent<Image>();

    if (image != null)
        image.color = selectedColor;

    SetConfirmCancelInteractable(true);
}

// =========================================================
// CONFIRM HOST
// =========================================================

private void OnConfirmHost()
{
    if (selectedCandidateId == -1)
    {
        Debug.LogWarning(
            "[HostCandidatesScreen] " +
            "Confirm pressed with no candidate."
        );

        return;
    }

    if (waitingForHostResponse)
        return;

    waitingForHostResponse = true;

    SetConfirmCancelInteractable(false);

    Debug.Log(
        "[HostCandidatesScreen] " +
        "Host confirmed. Hiding screen visuals while robot speaks."
    );

    // IMPORTANT:
    // Do NOT call flowManager.GoTo() here.
    //
    // The backend response will arrive AFTER the robot finishes
    // speaking. We must remain subscribed to OnSessionUpdate.
    HideVisuals();

    if (SessionManager.Instance != null)
    {
        SessionManager.Instance.SelectHost(
            selectedCandidateId
        );
    }
    else
    {
        Debug.LogError(
            "[HostCandidatesScreen] " +
            "SessionManager.Instance is NULL."
        );

        waitingForHostResponse = false;
        ShowVisuals();
    }
}

// =========================================================
// CANCEL
// =========================================================

private void OnCancelHost()
{
    if (waitingForHostResponse)
        return;

    selectedCandidateId = -1;
    selectedCandidateButtonObj = null;

    SetConfirmCancelInteractable(false);

    if (flowManager != null)
    {
        flowManager.GoTo(
            VisitorFlowState.MeetSomeone_EnterHostName
        );
    }
}

// =========================================================
// SESSION RESPONSE
// =========================================================

private void HandleSessionUpdate(
    SessionResponse response)
{
    if (!isActiveAndEnabled ||
        response == null)
    {
        return;
    }

    Debug.Log(
        $"[HostCandidatesScreen] " +
        $"Received response: {response.state}"
    );

    // -----------------------------------------------------
    // Updated candidate list
    // -----------------------------------------------------

    if (response.state == "HOST_SELECTION" ||
        response.state == "HOST_SUGGESTIONS")
    {
        waitingForHostResponse = false;

        ShowVisuals();

        if (flowManager != null &&
            flowManager.Session != null)
        {
            flowManager.Session.hostCandidates =
                response.host_candidates;

            ShowHostCandidates(
                response.host_candidates
            );
        }

        return;
    }

    // -----------------------------------------------------
    // Host confirmed -> purpose
    // -----------------------------------------------------

    if (response.state == "AWAITING_PURPOSE" ||
        response.state == "AWAITING_INTENT" ||
        response.state == "ANYTHING_ELSE" ||
        response.state == "QUERY_ANSWERED")
    {
        Debug.Log(
            "[HostCandidatesScreen] " +
            "Host accepted. Moving to ConversationScreen."
        );

        if (flowManager != null)
        {
            flowManager.GoTo(
                VisitorFlowState.MeetSomeone_EnterPurpose
            );
        }

        return;
    }

    // -----------------------------------------------------
    // Photo requested
    // -----------------------------------------------------

    if (response.state == "AWAITING_PHOTO")
    {
        if (flowManager != null)
        {
            if (flowManager.Session != null &&
                response.matched_host != null)
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

    // -----------------------------------------------------
    // Handoff
    // -----------------------------------------------------

    if (response.state == "READY_FOR_HANDOFF")
    {
        if (flowManager != null)
        {
            if (flowManager.Session != null)
            {
                if (response.matched_host != null)
                {
                    flowManager.Session.hostName =
                        response.matched_host.name;
                }

                flowManager.Session.qrBase64 =
                    response.qr_base64;
            }

            flowManager.GoTo(
                VisitorFlowState.MeetSomeone_ShowSimilarNames
            );
        }

        return;
    }

    // -----------------------------------------------------
    // Fallback
    // -----------------------------------------------------

    Debug.LogWarning(
        "[HostCandidatesScreen] " +
        $"Unhandled response state: {response.state}"
    );
}

// =========================================================
// RECORDING FAILED
// =========================================================

private void HandleRecordingFailed()
{
    if (!isActiveAndEnabled)
        return;

    Debug.LogWarning(
        "[HostCandidatesScreen] " +
        "Recording failed."
    );

    waitingForHostResponse = false;

    ShowVisuals();

    if (face != null)
    {
        face.SetExpression(
            FaceExpression.Confused
        );
    }
}
}