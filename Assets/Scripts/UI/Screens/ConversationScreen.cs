using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class ConversationScreen : MonoBehaviour
{
    public TextMeshProUGUI heardText;
    public GameObject listeningIndicator;
    public GameObject hostCandidatesPanel;
    public Transform hostCandidatesContainer;
    public GameObject hostCandidateButtonPrefab;
    public Button confirmHostButton;
    public Button cancelHostButton;
    public GameObject nameConfirmationPanel;
    public TMP_InputField nameInputField;
    public Button submitNameButton;
    public VisitorFlowManager flowManager;
    public FaceExpressionController face;

    private int selectedCandidateId = -1;
    private GameObject selectedCandidateButtonObj = null;
    private readonly Color selectedColor = new Color(0.75f, 0.85f, 1f);
    private Color defaultCandidateColor = Color.white;

    void OnEnable()
    {
        SessionManager.Instance.OnSessionUpdate += HandleSessionUpdate;
        SessionManager.Instance.OnRecordingFailed += HandleRecordingFailed;
        SessionManager.Instance.OnReadyToSpeak += HandleReadyToSpeak;
        SessionManager.Instance.OnRobotSpeaking += HandleRobotSpeakingForIndicator;

        if (confirmHostButton != null)
            confirmHostButton.onClick.AddListener(OnConfirmHost);
        if (cancelHostButton != null)
            cancelHostButton.onClick.AddListener(OnCancelHost);

        ResetScreen();
        StartListening();
    }

    void OnDisable()
    {
        // Stop any recording/send cycle still in flight so a late result
        // can't reach a session that's already moved past this screen
        // (this is what was causing "Not expecting audio in state: READY_FOR_HANDOFF").
        if (SessionManager.Instance != null)
            SessionManager.Instance.CancelPendingRecording();

        SessionManager.Instance.OnSessionUpdate -= HandleSessionUpdate;
        SessionManager.Instance.OnRecordingFailed -= HandleRecordingFailed;
        SessionManager.Instance.OnReadyToSpeak -= HandleReadyToSpeak;
        SessionManager.Instance.OnRobotSpeaking -= HandleRobotSpeakingForIndicator;

        if (confirmHostButton != null)
            confirmHostButton.onClick.RemoveListener(OnConfirmHost);
        if (cancelHostButton != null)
            cancelHostButton.onClick.RemoveListener(OnCancelHost);
    }

    void ResetScreen()
    {
        hostCandidatesPanel.SetActive(false);
        nameConfirmationPanel.SetActive(false);
        listeningIndicator.SetActive(false);
        heardText.text = "";
        selectedCandidateId = -1;
        selectedCandidateButtonObj = null;
        SetConfirmCancelInteractable(false);
    }

    void SetConfirmCancelInteractable(bool interactable)
    {
        if (confirmHostButton != null)
            confirmHostButton.interactable = interactable;
        if (cancelHostButton != null)
            cancelHostButton.interactable = interactable;
    }

    void StartListening()
    {
        SessionManager.Instance.RecordAndSend();
    }

    void HandleReadyToSpeak()
    {
        listeningIndicator.SetActive(true);
    }

    void HandleSessionUpdate(SessionResponse response)
    {
        if (!isActiveAndEnabled) return;

        listeningIndicator.SetActive(false);
        heardText.text = string.IsNullOrEmpty(response.heard_text) ? "" : $"You said: {response.heard_text}";

        switch (response.state)
        {
            case "HOST_SELECTION":
            case "HOST_SUGGESTIONS":
                ShowHostCandidates(response.host_candidates);
                break;

            case "NAME_CONFIRMATION":
                ShowNameConfirmation(response.heard_text);
                break;

            case "AWAITING_PURPOSE":
            case "AWAITING_NAME":
            case "AWAITING_HOST_NAME":
            case "AWAITING_INTENT":
            case "ANYTHING_ELSE":
                StartListening();
                break;

            case "QUERY_ANSWERED":
                StartListening();
                break;

            case "FALLBACK":
                StartListening();
                break;

            case "READY_FOR_HANDOFF":
                if (response.matched_host != null && flowManager.Session != null)
                    flowManager.Session.hostName = response.matched_host.name;

                if (flowManager.Session != null)
                    flowManager.Session.qrBase64 = response.qr_base64;

                flowManager.GoTo(VisitorFlowState.MeetSomeone_ShowSimilarNames);
                break;

            case "AWAITING_PHOTO":
                if (response.matched_host != null && flowManager.Session != null)
                    flowManager.Session.hostName = response.matched_host.name;

                flowManager.GoTo(VisitorFlowState.CapturePhoto);
                break;
            default:
                Debug.LogWarning("Unhandled session state:" + response.state);
                heardText.text = "Sorry, I didn't understand that. Please try again.";
                if (face != null) face.SetExpression(FaceExpression.Confused);
                StartListening();
                break;
        }
    }

    void ShowHostCandidates(HostCandidate[] candidates)
    {
        if (candidates == null || candidates.Length == 0)
        {
            hostCandidatesPanel.SetActive(false);
            StartListening();
            return;
        }

        hostCandidatesPanel.SetActive(true);
        selectedCandidateId = -1;
        selectedCandidateButtonObj = null;
        SetConfirmCancelInteractable(false);

        foreach (Transform child in hostCandidatesContainer)
            Destroy(child.gameObject);

        foreach (var candidate in candidates)
        {
            GameObject btnObj = Instantiate(hostCandidateButtonPrefab, hostCandidatesContainer);
            btnObj.GetComponentInChildren<TextMeshProUGUI>().text = candidate.name;

            int id = candidate.id;
            btnObj.GetComponent<Button>().onClick.AddListener(() => OnCandidateSelected(id, btnObj));
        }
    }

    void OnCandidateSelected(int employeeId, GameObject btnObj)
    {
        if (selectedCandidateButtonObj != null)
        {
            var prevImage = selectedCandidateButtonObj.GetComponent<Image>();
            if (prevImage != null) prevImage.color = defaultCandidateColor;
        }

        selectedCandidateId = employeeId;
        selectedCandidateButtonObj = btnObj;

        var image = btnObj.GetComponent<Image>();
        if (image != null) image.color = selectedColor;

        SetConfirmCancelInteractable(true);
    }

    void OnConfirmHost()
    {
        if (selectedCandidateId == -1)
        {
            Debug.LogWarning("Confirm pressed with no candidate selected.");
            return;
        }

        hostCandidatesPanel.SetActive(false);
        SetConfirmCancelInteractable(false);
        SessionManager.Instance.SelectHost(selectedCandidateId);
    }

    void OnCancelHost()
    {
        hostCandidatesPanel.SetActive(false);
        selectedCandidateId = -1;
        selectedCandidateButtonObj = null;
        SetConfirmCancelInteractable(false);
        heardText.text = "";
        StartListening();
    }

    void ShowNameConfirmation(string heardName)
    {
        nameConfirmationPanel.SetActive(true);
        nameInputField.text = heardName ?? "";
    }

    public void OnSubmitName()
    {
        string finalName = nameInputField.text.Trim();

        if (string.IsNullOrEmpty(finalName))
        {
            Debug.LogWarning("Name field empty — not submitting.");
            return;
        }

        nameConfirmationPanel.SetActive(false);
        SessionManager.Instance.SubmitName(finalName);
    }

    void HandleRecordingFailed()
    {
        if (!isActiveAndEnabled) return;

        heardText.text = "Didn't catch that. Please try again.";
        if (face != null) face.SetExpression(FaceExpression.Confused);
        StartListening();
    }

    void HandleRobotSpeakingForIndicator(string text)
    {
        listeningIndicator.SetActive(false);
    }
}