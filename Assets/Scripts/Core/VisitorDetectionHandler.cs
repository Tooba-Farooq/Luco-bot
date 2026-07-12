using UnityEngine;

public class VisitorDetectionHandler : MonoBehaviour
{
    public FaceDetectionService detectionService;
    public FaceExpressionController face;
    public AudioClip unknownGreetingClip; // hardcoded "How may I help you?" audio
    public VisitorFlowManager flowManager;
    private string lastStatus = "";


    void OnEnable()
    {
        detectionService.OnDetectionResult += HandleResult;
    }

    void OnDisable()
    {
        detectionService.OnDetectionResult -= HandleResult;
    }

    void HandleResult(DetectResponse result)
    {
        Debug.Log($"Detect result: status={result.status}, face_forward={result.face_forward}, duration={result.forward_duration}");

        if (result.status == lastStatus) return;
        lastStatus = result.status;
        bool flowIsIdle = flowManager.CurrentState == VisitorFlowState.Idle
                        || flowManager.CurrentState == VisitorFlowState.DetectingPerson;
        
         if (!flowIsIdle)
        {
            // Ignore — visitor is already in the middle of a conversation
            return;
        }
        
        switch (result.status)
        {
            case "idle":
                face.ReturnToIdle();
                break;

            case "detecting":
                // stay idle, no audio — could add a subtle "noticing" expression later if wanted
                break;

            case "unknown":
                face.SetExpression(FaceExpression.Happy); // greeting reaction
                face.StartTalking(unknownGreetingClip);   // hardcoded "How may I help you?" for now
                flowManager.GoTo(VisitorFlowState.CollectName);
                break;

            case "known":
                face.SetExpression(FaceExpression.Happy);
                // Later: dynamically greet using result.visitor_name (once backend sends real names)
                // e.g. generate/greet audio with name, or trigger a personalized flow state
                break;
        }
    }
}