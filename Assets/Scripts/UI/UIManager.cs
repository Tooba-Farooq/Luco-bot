using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    public GameObject screenConversation;
    public GameObject screenCapturePhoto;
    public GameObject screenQRCode;

    public GameObject screenAskHostName;
    public GameObject screenConfirmHostName;
    public GameObject screenAskVisitorName;
    public GameObject screenConfirmSpelling;
    public GameObject screenCorrectName;

    private Dictionary<VisitorFlowState, GameObject> screenMap;
    private GameObject currentScreenObj;
    private ScreenTransition currentTransition; // null if the current screen has no animation

    void Awake()
    {
        screenMap = new Dictionary<VisitorFlowState, GameObject>
        {
            { VisitorFlowState.MeetSomeone_EnterHostName, screenConversation },
            { VisitorFlowState.CapturePhoto,               screenCapturePhoto },
            { VisitorFlowState.MeetSomeone_ShowSimilarNames, screenQRCode },
        };

        screenConversation.SetActive(false);
        screenCapturePhoto.SetActive(false);
        screenQRCode.SetActive(false);
        ForceOffLegacyScreens();
    }

    public void ShowScreen(VisitorFlowState state)
    {
        ForceOffLegacyScreens();

        if (!screenMap.TryGetValue(state, out GameObject next))
        {
            HideCurrent();
            return;
        }

        ScreenTransition nextTransition = next.GetComponent<ScreenTransition>();

        if (nextTransition != null)
            StartCoroutine(TransitionTo(next, nextTransition));
        else
            ShowInstant(next);
    }

    private void ShowInstant(GameObject next)
    {
        HideCurrent();
        next.SetActive(true);
        currentScreenObj = next;
        currentTransition = null;
    }

    private void HideCurrent()
    {
        if (currentScreenObj == null) return;

        if (currentTransition != null)
            currentTransition.PlayHide(); // animated screens still hide themselves via their own Hide clip
        else
            currentScreenObj.SetActive(false); // instant screens (e.g. Capture Photo) just switch off

        currentScreenObj = null;
        currentTransition = null;
    }

    private IEnumerator TransitionTo(GameObject next, ScreenTransition nextTransition)
    {
        if (currentScreenObj != null && currentScreenObj != next)
        {
            if (currentTransition != null)
            {
                bool hideDone = false;
                currentTransition.OnHideComplete += () => hideDone = true;
                currentTransition.PlayHide();
                yield return new WaitUntil(() => hideDone);
            }
            else
            {
                currentScreenObj.SetActive(false);
            }
        }

        nextTransition.PlayShow();
        currentScreenObj = next;
        currentTransition = nextTransition;
    }

    private void ForceOffLegacyScreens()
    {
        if (screenAskHostName != null) screenAskHostName.SetActive(false);
        if (screenConfirmHostName != null) screenConfirmHostName.SetActive(false);
        if (screenAskVisitorName != null) screenAskVisitorName.SetActive(false);
        if (screenConfirmSpelling != null) screenConfirmSpelling.SetActive(false);
        if (screenCorrectName != null) screenCorrectName.SetActive(false);
    }
}