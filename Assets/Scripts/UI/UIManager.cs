using UnityEngine;

public class UIManager : MonoBehaviour
{
    public GameObject screenConversation; // NEW — replaces the old multi-screen conversation flow
    public GameObject screenCapturePhoto;
    public GameObject screenQRCode;

    // Legacy hardcoded-flow screens — no longer driven by ShowScreen(),
    // kept only so nothing else in the project breaks if still referenced elsewhere.
    // Safe to delete once you've confirmed nothing else uses them.
    public GameObject screenAskHostName;
    public GameObject screenConfirmHostName;
    public GameObject screenAskVisitorName;
    public GameObject screenConfirmSpelling;
    public GameObject screenCorrectName;

    public void ShowScreen(VisitorFlowState state)
    {
        screenConversation.SetActive(false);
        screenCapturePhoto.SetActive(false);
        screenQRCode.SetActive(false);

        // Legacy screens forced off too, in case they're still active from before this update
        if (screenAskHostName != null) screenAskHostName.SetActive(false);
        if (screenConfirmHostName != null) screenConfirmHostName.SetActive(false);
        if (screenAskVisitorName != null) screenAskVisitorName.SetActive(false);
        if (screenConfirmSpelling != null) screenConfirmSpelling.SetActive(false);
        if (screenCorrectName != null) screenCorrectName.SetActive(false);

        switch (state)
        {
            case VisitorFlowState.MeetSomeone_EnterHostName:
                // This state now means "enter the conversation loop" — the whole
                // purpose/host/name flow happens inside Screen_Conversation itself.
                screenConversation.SetActive(true);
                break;

            case VisitorFlowState.CapturePhoto:
                screenCapturePhoto.SetActive(true);
                break;

            case VisitorFlowState.MeetSomeone_ShowSimilarNames:
                screenQRCode.SetActive(true);
                break;
        }
    }
}