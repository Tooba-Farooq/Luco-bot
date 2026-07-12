using UnityEngine;

public class UIManager : MonoBehaviour
{
    public GameObject screenCollectName;
    public GameObject screenCapturePhoto; 
    public GameObject screenAskPurpose;
    public GameObject screenEnterHostName;
    public GameObject screenEnterPurpose;
    public GameObject screenWaitingHostResponse;
    public GameObject screenHostUnavailable;
    public GameObject screenVisitorWaiting;
    public GameObject screenRecordMessage;
    public GameObject screenVisitLogged;

    // add more screen references as you build them

    public void ShowScreen(VisitorFlowState state)
    {
        // Hide all screens first
        screenCollectName.SetActive(false);
        screenCapturePhoto.SetActive(false);
        screenAskPurpose.SetActive(false);
        screenEnterHostName.SetActive(false);
        screenEnterPurpose.SetActive(false);
        screenWaitingHostResponse.SetActive(false);
        screenHostUnavailable.SetActive(false);
        screenVisitorWaiting.SetActive(false);
        screenRecordMessage.SetActive(false);
        screenVisitLogged.SetActive(false);

        switch (state)
        {
            case VisitorFlowState.CollectName:
                screenCollectName.SetActive(true);
                screenCollectName.GetComponent<CollectNameScreen>().ResetScreen();
                break;

            case VisitorFlowState.CapturePhoto:
                screenCapturePhoto.SetActive(true);
                break;

            case VisitorFlowState.AskPurpose:
            case VisitorFlowState.IntentBranch:
                screenAskPurpose.SetActive(true);
                break;
            
            case VisitorFlowState.MeetSomeone_EnterHostName:
                screenEnterHostName.SetActive(true);
                screenEnterHostName.GetComponent<EnterHostNameScreen>().ResetScreen();
                break;
            
            case VisitorFlowState.MeetSomeone_EnterPurpose:
                screenEnterPurpose.SetActive(true);
                screenEnterPurpose.GetComponent<EnterPurposeScreen>().ResetScreen();
                break;

            case VisitorFlowState.AlertingHost:
            case VisitorFlowState.WaitingHostResponse:
                screenWaitingHostResponse.SetActive(true);
                break;

            case VisitorFlowState.HostUnavailable:
                screenHostUnavailable.SetActive(true);
                break;
            
            case VisitorFlowState.VisitorWaiting:
                screenVisitorWaiting.SetActive(true);
                break;

            case VisitorFlowState.RecordMessage:
                screenRecordMessage.SetActive(true);
                break;
            
            case VisitorFlowState.VisitLogged:
                screenVisitLogged.SetActive(true);
                break;
        }   
    }
}