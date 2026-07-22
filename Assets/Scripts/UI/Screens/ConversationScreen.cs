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
    public GameObject nameConfirmationPanel;
    public TMP_InputField nameInputField;
    public Button submitNameButton;
    public VisitorFlowManager flowManager;

    void OnEnable()
    {
        SessionManager.Instance.OnSessionUpdate += HandleSessionUpdate;
        SessionManager.Instance.OnRecordingFailed +=HandleRecordingFailed;
        ResetScreen();
        StartListening();
    }

    void OnDisable()
    {
        SessionManager.Instance.OnSessionUpdate -= HandleSessionUpdate;
        SessionManager.Instance.OnRecordingFailed -= HandleRecordingFailed;
    }

    void ResetScreen()
    {
        hostCandidatesPanel.SetActive(false);
        nameConfirmationPanel.SetActive(false);
        listeningIndicator.SetActive(false);
        heardText.text = "";
    }

    void StartListening()
    {
        listeningIndicator.SetActive(true);
        SessionManager.Instance.RecordAndSend();
    }

    void HandleSessionUpdate(SessionResponse response)
    {
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
                StartListening(); // continue the conversation loop
                break;

            case "QUERY_ANSWERED":
                StartListening(); // continue after answering a general question
                break;

            case "FALLBACK":
                StartListening(); // retry listening
                break;

            case "READY_FOR_HANDOFF":
                if (response.matched_host != null && flowManager.Session != null)
                    flowManager.Session.hostName = response.matched_host.name;

                flowManager.GoTo(VisitorFlowState.MeetSomeone_ShowSimilarNames);
                break;
            
            case "AWAITING_PHOTO":
                if (response.matched_host != null && flowManager.Session != null)
                    flowManager.Session.hostName = response.matched_host.name;

                flowManager.GoTo(VisitorFlowState.CapturePhoto);
                break;
            default:
                Debug.LogWarning("Unhandled session state:" + response.state);
                heardText.text ="Sorry, I didn't understand that. Please try again.";
                StartListening();
                break;
        }
    }

    void ShowHostCandidates(HostCandidate[] candidates)
    {
        hostCandidatesPanel.SetActive(true);

        foreach (Transform child in hostCandidatesContainer)
            Destroy(child.gameObject);

        if (candidates == null) return;

        foreach (var candidate in candidates)
        {
            GameObject btnObj = Instantiate(hostCandidateButtonPrefab, hostCandidatesContainer);
            btnObj.GetComponentInChildren<TextMeshProUGUI>().text = candidate.name;

            int id = candidate.id;
            btnObj.GetComponent<Button>().onClick.AddListener(() => OnCandidateSelected(id));
        }
    }

    void OnCandidateSelected(int employeeId)
    {
        hostCandidatesPanel.SetActive(false);
        SessionManager.Instance.SelectHost(employeeId);
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
            return; // don't let them submit blank
        }

        nameConfirmationPanel.SetActive(false);
        SessionManager.Instance.SubmitName(finalName);
    }

    void HandleRecordingFailed()
    {
        // Didn't catch anything — just listen again rather than getting stuck
        heardText.text = "Didn't catch that. Please try again.";
        StartListening();
    }
}