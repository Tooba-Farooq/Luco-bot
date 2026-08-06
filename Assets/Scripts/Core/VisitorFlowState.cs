
public enum VisitorFlowState
{
    Idle,

    DetectingPerson,

    FaceRecognition,

    GreetKnownVisitor,

    CollectName,

    ConfirmSpelling,

    CapturePhoto,

    AskPurpose,

    IntentBranch,

    GeneralQuery,

    // Conversation screen — asking/receiving host name
    MeetSomeone_EnterHostName,

    // Conversation screen — host lookup/confirmation
    MeetSomeone_HostLookup,

    // Standalone host candidates screen
    HostCandidatesSelection,

    // QR code / handoff screen
    MeetSomeone_ShowSimilarNames,

    // Conversation screen — purpose
    MeetSomeone_EnterPurpose,

    // Standalone visitor name confirmation screen
    NameConfirmation,

    AlertingHost,

    WaitingHostResponse,

    HostUnavailable,

    HostAccepted,

    VisitorWaiting,

    RecordMessage,

    VisitLogged
}

