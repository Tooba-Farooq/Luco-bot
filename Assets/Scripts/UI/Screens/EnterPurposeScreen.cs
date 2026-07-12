using UnityEngine;
using TMPro;

public class EnterPurposeScreen : MonoBehaviour
{
    [Header("UI References")]
    public GameObject typeButton;
    public GameObject voiceButton;
    public TMP_InputField purposeInputField;
    public GameObject submitButton;
    public GameObject listeningIndicator;

    [Header("Dependencies")]
    public VisitorFlowManager flowManager;
    public FaceExpressionController face;

    public void OnTypeSelected()
    {
        typeButton.SetActive(false);
        voiceButton.SetActive(false);
        purposeInputField.gameObject.SetActive(true);
        submitButton.SetActive(true);
        purposeInputField.text = "";
        purposeInputField.Select();
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
        string purpose = purposeInputField.text.Trim();

        if (string.IsNullOrEmpty(purpose))
        {
            Debug.LogWarning("Purpose field empty, not submitting.");
            return;
        }

        if (flowManager == null)
        {
            Debug.LogError("CRITICAL: flowManager field is BLANK in Screen_EnterPurpose Inspector!");
            return;
        }

        flowManager.Session.purpose = purpose;
        Debug.Log("Purpose captured: " + purpose);

        if (face != null)
            face.SetExpression(FaceExpression.Thinking, autoReturnToIdle: false);

        flowManager.GoTo(VisitorFlowState.AlertingHost);
    }

    public void ResetScreen()
    {
        typeButton.SetActive(true);
        voiceButton.SetActive(true);
        purposeInputField.gameObject.SetActive(false);
        submitButton.SetActive(false);
        listeningIndicator.SetActive(false);
    }
}