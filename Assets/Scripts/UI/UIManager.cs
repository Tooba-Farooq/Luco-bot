using UnityEngine;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    [Header("Main Screens")]
    public GameObject screenConversation;
    public GameObject screenCapturePhoto;
    public GameObject screenQRCode;
    public GameObject screenHostCandidates;
    public GameObject screenNameConfirmation;

    [Header("Legacy Screens")]
    public GameObject screenAskHostName;
    public GameObject screenConfirmHostName;
    public GameObject screenAskVisitorName;
    public GameObject screenConfirmSpelling;
    public GameObject screenCorrectName;

    private Dictionary<VisitorFlowState, GameObject> screenMap;

    private GameObject currentScreen;

    void Awake()
    {
        screenMap = new Dictionary<VisitorFlowState, GameObject>
        {
            // Conversation
            {
                VisitorFlowState.MeetSomeone_EnterHostName,
                screenConversation
            },

            {
                VisitorFlowState.MeetSomeone_HostLookup,
                screenConversation
            },

            {
                VisitorFlowState.MeetSomeone_EnterPurpose,
                screenConversation
            },

            {
                VisitorFlowState.AskPurpose,
                screenConversation
            },

            {
                VisitorFlowState.GeneralQuery,
                screenConversation
            },

            {
                VisitorFlowState.CollectName,
                screenConversation
            },

            // Host candidates
            {
                VisitorFlowState.HostCandidatesSelection,
                screenHostCandidates
            },

            // Name confirmation
            {
                VisitorFlowState.NameConfirmation,
                screenNameConfirmation
            },

            // Photo
            {
                VisitorFlowState.CapturePhoto,
                screenCapturePhoto
            },

            // QR
            {
                VisitorFlowState.MeetSomeone_ShowSimilarNames,
                screenQRCode
            }
        };

        HideAllScreens();
    }

    // =========================================================
    // SHOW SCREEN
    // =========================================================

    public void ShowScreen(VisitorFlowState state)
{
    Debug.Log($"[UIManager] Showing state: {state}");

    if (!screenMap.TryGetValue(state, out GameObject nextScreen))
    {
        Debug.LogWarning($"[UIManager] No screen mapped for state: {state}");
        HideAllScreens();
        currentScreen = null;
        return;
    }

    if (nextScreen == null)
    {
        Debug.LogError($"[UIManager] Screen reference is NULL for state: {state}");
        HideAllScreens();
        currentScreen = null;
        return;
    }

    // Same screen as before — don't toggle it off/on.
    if (nextScreen == currentScreen)
    {
        Debug.Log($"[UIManager] Staying on same screen: {nextScreen.name}");
        return;
    }

    HideAllScreens();

    nextScreen.SetActive(true);
    currentScreen = nextScreen;

    Debug.Log($"[UIManager] ACTIVE SCREEN = {nextScreen.name}");
}

    // =========================================================
    // HIDE EVERYTHING
    // =========================================================

    public void HideAllScreens()
    {
        SetScreenActive(screenConversation, false);
        SetScreenActive(screenCapturePhoto, false);
        SetScreenActive(screenQRCode, false);
        SetScreenActive(screenHostCandidates, false);
        SetScreenActive(screenNameConfirmation, false);

        // Legacy screens
        SetScreenActive(screenAskHostName, false);
        SetScreenActive(screenConfirmHostName, false);
        SetScreenActive(screenAskVisitorName, false);
        SetScreenActive(screenConfirmSpelling, false);
        SetScreenActive(screenCorrectName, false);

        currentScreen = null;
    }

    private void SetScreenActive(
        GameObject screen,
        bool active)
    {
        if (screen != null)
        {
            screen.SetActive(active);
        }
    }

    // =========================================================
    // DEBUG
    // =========================================================

    public GameObject GetCurrentScreen()
    {
        return currentScreen;
    }
}

