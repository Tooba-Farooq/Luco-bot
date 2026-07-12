using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CollectNameScreen : MonoBehaviour
{
    [Header("UI References")]
    public GameObject typeButton;
    public GameObject voiceButton;
    public TMP_InputField nameInputField;
    public GameObject submitButton;
    public GameObject listeningIndicator;

    [Header("Dependencies")]
    public VisitorFlowManager flowManager;
    public FaceExpressionController face;

    public void OnTypeSelected()
    {
        typeButton.SetActive(false);
        voiceButton.SetActive(false);
        nameInputField.gameObject.SetActive(true);
        submitButton.SetActive(true);
        nameInputField.text = "";
        nameInputField.Select();
    }

    public void OnVoiceSelected()
    {
        typeButton.SetActive(false);
        voiceButton.SetActive(false);
        listeningIndicator.SetActive(true);

        if (face != null)
            face.SetExpression(FaceExpression.Listening, autoReturnToIdle: false);

        // STUB — real speech-to-text isn't wired up yet.
        // For now, fall back to typing after a short delay so the flow isn't a dead end.
        Invoke(nameof(FallbackToTyping), 2f);
    }

    private void FallbackToTyping()
    {
        listeningIndicator.SetActive(false);

        if (face != null)
            face.ReturnToIdle();

        OnTypeSelected(); // reuse the typing UI as fallback
    }

    public void OnSubmit()
    {
        string name = nameInputField.text.Trim();

        if (string.IsNullOrEmpty(name))
        {
            Debug.LogWarning("Name field empty, not submitting.");
            return;
        }

        // Defensive check to prevent code crashes
        if (flowManager == null)
        {
            Debug.LogError("CRITICAL: flowManager field is BLANK in the Screen_CollectName Inspector!");
            return;
        }

        if (flowManager.Session != null)
        {
            flowManager.Session.visitorName = name;
        }
        else
        {
            Debug.LogWarning("Session was null, skipping name assign.");
        }

        Debug.Log("Visitor name captured: " + name);

        if (face != null)
        {
            face.SetExpression(FaceExpression.Happy);
        }

        // Direct transition test
        Debug.Log("Attempting GoTo CapturePhoto...");
        flowManager.GoTo(VisitorFlowState.CapturePhoto);
    }

    // Called by UIManager when this screen becomes active — resets to initial choice state
    public void ResetScreen()
    {
        typeButton.SetActive(true);
        voiceButton.SetActive(true);
        nameInputField.gameObject.SetActive(false);
        submitButton.SetActive(false);
        listeningIndicator.SetActive(false);
    }
}