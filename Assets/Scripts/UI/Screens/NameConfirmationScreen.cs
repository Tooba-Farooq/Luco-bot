using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NameConfirmationScreen : MonoBehaviour
{
[Header("Name UI")]
public TMP_InputField nameInputField;
public Button submitNameButton;


[Header("Visual Root")]
[Tooltip("Optional. Assign the visual content of this screen, NOT the GameObject containing this script.")]
public GameObject visualRoot;

[Header("Flow")]
public VisitorFlowManager flowManager;

private bool waitingForResponse = false;

// =========================================================
// ENABLE
// =========================================================

void OnEnable()
{
    Debug.Log(
        "[NameConfirmationScreen] ENABLED"
    );

    if (SessionManager.Instance != null)
    {
        SessionManager.Instance.OnSessionUpdate +=
            HandleSessionUpdate;

        SessionManager.Instance.OnRecordingFailed +=
            HandleRecordingFailed;
    }

    if (submitNameButton != null)
    {
        submitNameButton.onClick.AddListener(
            OnSubmitName
        );
    }

    if (nameInputField != null)
    {
        nameInputField.onSubmit.AddListener(
            OnNameInputSubmitted
        );
    }

    waitingForResponse = false;

    ShowVisuals();

    // Read the name stored by ConversationScreen.
    if (flowManager != null &&
        flowManager.Session != null)
    {
        SetName(
            flowManager.Session.visitorName
        );
    }
}

// =========================================================
// DISABLE
// =========================================================

void OnDisable()
{
    if (SessionManager.Instance != null)
    {
        SessionManager.Instance.OnSessionUpdate -=
            HandleSessionUpdate;

        SessionManager.Instance.OnRecordingFailed -=
            HandleRecordingFailed;
    }

    if (submitNameButton != null)
    {
        submitNameButton.onClick.RemoveListener(
            OnSubmitName
        );
    }

    if (nameInputField != null)
    {
        nameInputField.onSubmit.RemoveListener(
            OnNameInputSubmitted
        );
    }

    waitingForResponse = false;
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
        // IMPORTANT:
        // Do not disable this GameObject.
        // This script must remain subscribed to
        // SessionManager.OnSessionUpdate.

        if (nameInputField != null)
            nameInputField.gameObject.SetActive(false);

        if (submitNameButton != null)
            submitNameButton.gameObject.SetActive(false);
    }

    Debug.Log(
        "[NameConfirmationScreen] " +
        "Visuals hidden while waiting for response."
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
        if (nameInputField != null)
            nameInputField.gameObject.SetActive(true);

        if (submitNameButton != null)
            submitNameButton.gameObject.SetActive(true);
    }
}

// =========================================================
// SET NAME
// =========================================================

private void SetName(string name)
{
    if (nameInputField == null)
        return;

    nameInputField.text =
        name ?? "";

    nameInputField.Select();
    nameInputField.ActivateInputField();

    if (submitNameButton != null)
    {
        submitNameButton.interactable =
            !string.IsNullOrWhiteSpace(name);
    }

    Debug.Log(
        $"[NameConfirmationScreen] " +
        $"Showing name: '{name}'"
    );
}

// =========================================================
// ENTER
// =========================================================

private void OnNameInputSubmitted(
    string text)
{
    OnSubmitName();
}

// =========================================================
// SUBMIT
// =========================================================

public void OnSubmitName()
{
    if (waitingForResponse)
        return;

    if (nameInputField == null)
        return;

    string finalName =
        nameInputField.text.Trim();

    if (string.IsNullOrEmpty(finalName))
    {
        Debug.LogWarning(
            "[NameConfirmationScreen] " +
            "Name is empty."
        );

        return;
    }

    waitingForResponse = true;

    if (submitNameButton != null)
    {
        submitNameButton.interactable = false;
    }

    if (flowManager != null &&
        flowManager.Session != null)
    {
        flowManager.Session.visitorName =
            finalName;
    }

    Debug.Log(
        $"[NameConfirmationScreen] " +
        $"Submitting name: '{finalName}'"
    );

    // IMPORTANT:
    // Do NOT GoTo(CapturePhoto) here.
    //
    // The backend still has to respond and the robot has to
    // say something such as "Let's go take your photo."
    //
    // Keep this GameObject active so it receives OnSessionUpdate,
    // but hide its visual contents immediately.
    HideVisuals();

    if (SessionManager.Instance != null)
    {
        SessionManager.Instance.SubmitName(
            finalName
        );
    }
    else
    {
        Debug.LogError(
            "[NameConfirmationScreen] " +
            "SessionManager.Instance is NULL."
        );

        waitingForResponse = false;
        ShowVisuals();

        if (submitNameButton != null)
            submitNameButton.interactable = true;
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
        $"[NameConfirmationScreen] " +
        $"Received response: {response.state}"
    );

    // -----------------------------------------------------
    // PHOTO
    // -----------------------------------------------------

    if (response.state == "AWAITING_PHOTO")
    {
        Debug.Log(
            "[NameConfirmationScreen] " +
            "Backend requested photo. " +
            "Opening CapturePhotoScreen."
        );

        if (flowManager != null &&
            flowManager.Session != null)
        {
            if (response.matched_host != null)
            {
                flowManager.Session.hostName =
                    response.matched_host.name;
            }
        }

        if (flowManager != null)
        {
            flowManager.GoTo(
                VisitorFlowState.CapturePhoto
            );
        }

        return;
    }

    // -----------------------------------------------------
    // HANDOFF
    // -----------------------------------------------------

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
        }

        if (flowManager != null)
        {
            flowManager.GoTo(
                VisitorFlowState.MeetSomeone_ShowSimilarNames
            );
        }

        return;
    }

    // -----------------------------------------------------
    // NAME STILL NEEDS WORK
    // -----------------------------------------------------

    if (response.state == "NAME_CONFIRMATION")
    {
        waitingForResponse = false;

        ShowVisuals();

        if (flowManager != null &&
            flowManager.Session != null)
        {
            flowManager.Session.visitorName =
                response.heard_text;

            SetName(
                response.heard_text
            );
        }

        return;
    }

    // -----------------------------------------------------
    // OTHER CONVERSATION STATE
    // -----------------------------------------------------

    if (response.state == "AWAITING_PURPOSE" ||
        response.state == "AWAITING_NAME" ||
        response.state == "AWAITING_HOST_NAME" ||
        response.state == "AWAITING_INTENT")
    {
        Debug.Log(
            "[NameConfirmationScreen] " +
            "Moving back to ConversationScreen."
        );

        if (flowManager != null)
        {
            flowManager.GoTo(
                VisitorFlowState.MeetSomeone_EnterHostName
            );
        }

        return;
    }

    Debug.LogWarning(
        "[NameConfirmationScreen] " +
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

    waitingForResponse = false;

    ShowVisuals();

    if (submitNameButton != null)
    {
        submitNameButton.interactable = true;
    }

    Debug.LogWarning(
        "[NameConfirmationScreen] " +
        "Request failed."
    );
}


}
