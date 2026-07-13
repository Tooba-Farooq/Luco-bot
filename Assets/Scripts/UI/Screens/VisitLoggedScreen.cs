using UnityEngine;
using System.Collections;
using TMPro;

public class VisitLoggedScreen : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI thankYouText;

    [Header("Dependencies")]
    [SerializeField] private VisitorFlowManager flowManager;
    [SerializeField] private FaceExpressionController face;

    [Header("Timing")]
    [SerializeField] private float displayDuration = 4f;

    // Cache to safely manage and prevent overlapping or ghost coroutines
    private Coroutine timeoutRoutine;
    private FaceDetectionService cachedDetectionService;

    private void Awake()
    {
        // Cache the service reference once to eliminate expensive Find calls later
        cachedDetectionService = Object.FindAnyObjectByType<FaceDetectionService>();
        
        if (cachedDetectionService == null)
        {
            Debug.LogWarning("[VisitLoggedScreen] FaceDetectionService not found in scene. Polling will not auto-resume.");
        }
    }

    private void OnEnable()
    {
        if (flowManager == null)
        {
            Debug.LogError("CRITICAL: flowManager field is BLANK in Screen_VisitLogged Inspector!", this);
            return;
        }

        if (thankYouText == null)
        {
            Debug.LogError("CRITICAL: thankYouText field is BLANK in Screen_VisitLogged Inspector!", this);
            return;
        }

        // Build dynamic greeting text string safely
        string visitorName = flowManager.Session?.visitorName;
        string displayName = string.IsNullOrEmpty(visitorName) ? "there" : visitorName;
        thankYouText.text = $"Thank you, {displayName}! Have a great day.";

        // Set visual presentation state
        if (face != null)
        {
            face.SetExpression(FaceExpression.Success);
        }

        Debug.Log($"[VisitLoggedScreen] Visit logged for: {displayName}. Starting {displayDuration}s countdown.");

        // Clean up any stale running routines before spinning up a new one
        StopTimeoutRoutine();
        timeoutRoutine = StartCoroutine(ReturnToIdleAfterDelay());
    }

    private void OnDisable()
    {
        // Absolute critical guard: Stop the countdown if the screen is deactivated 
        // by outside logic (e.g., administrator manual reset, timeout override)
        StopTimeoutRoutine();
    }

    private IEnumerator ReturnToIdleAfterDelay()
    {
        yield return new WaitForSeconds(displayDuration);
        
        // Nullify reference loop container track
        timeoutRoutine = null;

        // Order of operations matter: Resume polling FIRST, then transition states 
        // to minimize race conditions with network polling ticks.
        if (cachedDetectionService != null)
        {
            cachedDetectionService.StartPolling();
        }

        flowManager.GoTo(VisitorFlowState.Idle);
    }

    private void StopTimeoutRoutine()
    {
        if (timeoutRoutine != null)
        {
            StopCoroutine(timeoutRoutine);
            timeoutRoutine = null;
        }
    }
}