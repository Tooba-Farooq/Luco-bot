public enum VisitorFlowState
{
    Idle,
    DetectingPerson,
    FaceRecognition,
    GreetKnownVisitor,
    CollectName,              // NOTE: repurposed as "AskVisitorName" for hardcoded demo flow
    ConfirmSpelling,
    CapturePhoto,
    AskPurpose,
    IntentBranch,
    GeneralQuery,
    MeetSomeone_EnterHostName,   // NOTE: repurposed as "AskHostName"
    MeetSomeone_HostLookup,      // NOTE: repurposed as "ConfirmHostName"
    MeetSomeone_ShowSimilarNames,// NOTE: repurposed as "QRCode screen"
    MeetSomeone_EnterPurpose,
    AlertingHost,
    WaitingHostResponse,
    HostUnavailable,
    HostAccepted,
    VisitorWaiting,
    RecordMessage,
    VisitLogged
}