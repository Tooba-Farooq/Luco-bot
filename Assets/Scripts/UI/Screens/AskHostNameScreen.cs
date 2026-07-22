using UnityEngine;
using TMPro;
using System.Collections;

public class AskHostNameScreen : MonoBehaviour
{
    public TMP_InputField hostNameInputField;
    public VisitorFlowManager flowManager;
    public FaceExpressionController face;

    [Header("UI Groups")]
    // CHANGED: Drag your "bottom content" parent GameObject here in the Inspector
    public GameObject bottomContent; 

    [Header("Audio")]
    public AudioClip whoAreYouMeetingClip;
    public float delayBeforePrompt = 1.2f;

    private Coroutine promptCoroutine;

    void OnEnable()
    {
        hostNameInputField.text = "";

        // Hide the entire bottom UI group immediately when the screen turns on
        if (bottomContent != null)
        {
            bottomContent.SetActive(false);
        }

        StopPromptCoroutine();
        promptCoroutine = StartCoroutine(SpeakPromptAfterDelay());
    }

    void OnDisable()
    {
        StopPromptCoroutine();
    }

    private IEnumerator SpeakPromptAfterDelay()
    {
        yield return new WaitForSeconds(delayBeforePrompt);

        // Reveal the entire bottom group (text, input field, button) synced with audio
        if (bottomContent != null)
        {
            bottomContent.SetActive(true);
            
            // Move focus to the input field now that it's visible and active
            hostNameInputField.Select();
            hostNameInputField.ActivateInputField(); // Forces the carrot/focus to appear properly
        }

        if (face != null && whoAreYouMeetingClip != null)
        {
            face.StartTalking(whoAreYouMeetingClip);
        }
        
        promptCoroutine = null;
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
            Debug.LogError("CRITICAL: flowManager field is BLANK in Screen_HostEntry Inspector!");
            return;
        }

        StopPromptCoroutine(); 

        flowManager.Session.hostName = hostName;
        Debug.Log("Host name input saved: " + hostName);

        flowManager.GoTo(VisitorFlowState.MeetSomeone_HostLookup);
    }

    private void StopPromptCoroutine()
    {
        if (promptCoroutine != null)
        {
            StopCoroutine(promptCoroutine);
            promptCoroutine = null;
        }
    }
}