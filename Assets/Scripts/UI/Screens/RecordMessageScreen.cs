using UnityEngine;
using TMPro;

public class RecordMessageScreen : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI promptText;
    public GameObject typeButton;
    public GameObject voiceButton;
    public TMP_InputField messageInputField;
    public GameObject submitButton;
    public GameObject listeningIndicator;

    [Header("Dependencies")]
    public VisitorFlowManager flowManager;
    public FaceExpressionController face;

    void OnEnable()
    {
        if (flowManager == null)
        {
            Debug.LogError("CRITICAL: flowManager field is BLANK in Screen_RecordMessage Inspector!");
            return;
        }

        string hostName = flowManager.Session.hostName;
        promptText.text = $"Leave a message for {hostName}";

        ResetScreen();
    }

    public void OnTypeSelected()
    {
        typeButton.SetActive(false);
        voiceButton.SetActive(false);
        messageInputField.gameObject.SetActive(true);
        submitButton.SetActive(true);
        messageInputField.text = "";
        messageInputField.Select();
    }

    public void OnVoiceSelected()
    {
        typeButton.SetActive(false);
        voiceButton.SetActive(false);
        listeningIndicator.SetActive(true);

        if (face != null)
            face.SetExpression(FaceExpression.Listening, autoReturnToIdle: false);

        Invoke(nameof(FallbackToTyping), 2f);
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
        string message = messageInputField.text.Trim();

        if (string.IsNullOrEmpty(message))
        {
            Debug.LogWarning("Message field empty, not submitting.");
            return;
        }

        flowManager.Session.message = message;
        Debug.Log("Message captured: " + message);

        if (face != null)
            face.SetExpression(FaceExpression.Success);

        flowManager.GoTo(VisitorFlowState.VisitLogged);
    }

    public void OnCancel()
    {
        Debug.Log("Visitor cancelled message recording.");
        flowManager.GoTo(VisitorFlowState.VisitLogged);
    }

    public void ResetScreen()
    {
        typeButton.SetActive(true);
        voiceButton.SetActive(true);
        messageInputField.gameObject.SetActive(false);
        submitButton.SetActive(false);
        listeningIndicator.SetActive(false);
    }
}