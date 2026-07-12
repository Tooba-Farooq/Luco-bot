using UnityEngine;
using TMPro;

public class EnterHostNameScreen : MonoBehaviour
{
    [Header("UI References")]
    public GameObject typeButton;
    public GameObject voiceButton;
    public TMP_InputField hostNameInputField;
    public GameObject submitButton;
    public GameObject listeningIndicator;

    [Header("Dependencies")]
    public VisitorFlowManager flowManager;
    public FaceExpressionController face;

    public void OnTypeSelected()
    {
        typeButton.SetActive(false);
        voiceButton.SetActive(false);
        hostNameInputField.gameObject.SetActive(true);
        submitButton.SetActive(true);
        hostNameInputField.text = "";
        hostNameInputField.Select();
    }

    public void OnVoiceSelected()
    {
        typeButton.SetActive(false);
        voiceButton.SetActive(false);
        listeningIndicator.SetActive(true);

        if (face != null)
            face.SetExpression(FaceExpression.Listening, autoReturnToIdle: false);

        Invoke(nameof(FallbackToTyping), 2f); // stub, same as CollectName
    }

    private void FallbackToTyping()
    {
        listeningIndicator.SetActive(false);

        if (face != null)
            face.ReturnToIdle();

        OnTypeSelected();
    }

    public void OnSubmit()
    {
        string hostName = hostNameInputField.text.Trim();

        if (string.IsNullOrEmpty(hostName))
        {
            Debug.LogWarning("Host name field empty, not submitting.");
            return;
        }

        if (flowManager == null)
        {
            Debug.LogError("CRITICAL: flowManager field is BLANK in Screen_EnterHostName Inspector!");
            return;
        }

        flowManager.Session.hostName = hostName;
        Debug.Log("Host name captured: " + hostName);

        if (face != null)
            face.SetExpression(FaceExpression.Thinking, autoReturnToIdle: false);

        // This will eventually be a real backend lookup — for now just advance the flow
        flowManager.GoTo(VisitorFlowState.MeetSomeone_HostLookup);
    }

    public void ResetScreen()
    {
        typeButton.SetActive(true);
        voiceButton.SetActive(true);
        hostNameInputField.gameObject.SetActive(false);
        submitButton.SetActive(false);
        listeningIndicator.SetActive(false);
    }
}