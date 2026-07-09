using UnityEngine;
using System.Collections;

public enum FaceExpression
{
    Idle,
    Listening,
    Happy,
    Thinking,
    Apologetic,
    Success
}

public class FaceExpressionController : MonoBehaviour
{
    [Header("Face Parts")]
    public RectTransform eyeLeft;
    public RectTransform eyeRight;
    public RectTransform mouth;

    [Header("Blink Settings")]
    public float minBlinkInterval = 2f;
    public float maxBlinkInterval = 5f;
    public float blinkSpeed = 12f;

    [Header("Expression Transition Speed")]
    public float transitionSpeed = 6f;

    private FaceExpression currentExpression = FaceExpression.Idle;
    private Coroutine expressionRoutine;
    private Coroutine blinkLoopRoutine;
    private bool blinkingPaused = false;

    // Baseline (idle) values, captured at Start so every expression can return to them
    private Vector3 eyeLeftBaseScale, eyeRightBaseScale, mouthBaseScale;
    private Vector3 eyeLeftBasePos, eyeRightBasePos, mouthBasePos;
    private Vector3 eyeLeftBaseRot, eyeRightBaseRot;
    
    [Header("Talking")]
    public AudioSource audioSource;
    public AudioClip testClip;
    public RectTransform mouthTalk;
    public float mouthOpenMultiplier = 1.5f;
    public float talkSampleSmoothing = 8f;
    public float minMouthOpenScale = 0.3f;      
    public float maxMouthOpenScale = 1.2f;

    private Coroutine talkingRoutine;
    private float[] audioSampleData = new float[64];

    void Start()
    {
        eyeLeftBaseScale = eyeLeft.localScale;
        eyeRightBaseScale = eyeRight.localScale;
        mouthBaseScale = mouth.localScale;

        eyeLeftBasePos = eyeLeft.anchoredPosition3D;
        eyeRightBasePos = eyeRight.anchoredPosition3D;
        mouthBasePos = mouth.anchoredPosition3D;

        eyeLeftBaseRot = eyeLeft.localEulerAngles;
        eyeRightBaseRot = eyeRight.localEulerAngles;

        blinkLoopRoutine = StartCoroutine(BlinkLoop());
    }

    // ---------- PUBLIC API ----------

    public void SetExpression(FaceExpression expression, bool autoReturnToIdle = true, float holdDuration = 1.2f)
    {
        if (expressionRoutine != null) StopCoroutine(expressionRoutine);
        currentExpression = expression;
        expressionRoutine = StartCoroutine(PlayExpression(expression, autoReturnToIdle, holdDuration));
    }

    public void ReturnToIdle()
    {
        if (expressionRoutine != null) StopCoroutine(expressionRoutine);
        blinkingPaused = false;
        expressionRoutine = StartCoroutine(TransitionToBaseline());
        currentExpression = FaceExpression.Idle;
    }

    // ---------- BLINK ----------

    IEnumerator BlinkLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minBlinkInterval, maxBlinkInterval));
            Debug.Log($"Blink check — paused: {blinkingPaused}, currentExpression: {currentExpression}");
            if (!blinkingPaused && currentExpression != FaceExpression.Listening)
                yield return Blink();
        }
    }

    IEnumerator Blink()
    {
        Debug.Log("Blink triggered at " + Time.time);
        Vector3 openL = eyeLeft.localScale;
        Vector3 openR = eyeRight.localScale;
        Vector3 closedL = new Vector3(openL.x, 0.1f, openL.z);
        Vector3 closedR = new Vector3(openR.x, 0.1f, openR.z);

        float t = 0f;
        while (t < 1f) { t += Time.deltaTime * blinkSpeed; eyeLeft.localScale = Vector3.Lerp(openL, closedL, t); eyeRight.localScale = Vector3.Lerp(openR, closedR, t); yield return null; }
        t = 0f;
        while (t < 1f) { t += Time.deltaTime * blinkSpeed; eyeLeft.localScale = Vector3.Lerp(closedL, openL, t); eyeRight.localScale = Vector3.Lerp(closedR, openR, t); yield return null; }
    }

    // ---------- EXPRESSIONS ----------

    IEnumerator PlayExpression(FaceExpression expression, bool autoReturn, float holdDuration)
    {
        blinkingPaused = true;

        switch (expression)
        {
            case FaceExpression.Happy:
                yield return LerpToPose(
                    eyeScale: new Vector3(1f, 0.6f, 1f),
                    mouthScale: new Vector3(1.2f, 1f, 1f));
                break;

            case FaceExpression.Listening:
                yield return LerpToPose(
                    eyeScale: new Vector3(1.15f, 1.15f, 1f),
                    mouthScale: new Vector3(1f, 0.5f, 1f));
                blinkingPaused = false;
                yield break;

            case FaceExpression.Thinking:
                yield return LerpToPose(
                    eyePosOffsetL: new Vector3(0, 10f, 0),
                    eyePosOffsetR: new Vector3(0, 10f, 0));
                blinkingPaused = false;
                yield break;

            case FaceExpression.Apologetic:
                float originalSpeed= transitionSpeed;
                transitionSpeed= 3f;
                yield return LerpToPose(
                    eyeScale: new Vector3(1f, 0.5f, 1f),
                    eyePosOffsetL: new Vector3(0, -6f, 0),
                    eyePosOffsetR: new Vector3(0, -6f, 0),
                    mouthScale: new Vector3(1f, -0.8f, 1f),   // negative Y flips smile into frown
                    mouthPosOffset: new Vector3(0, -4f, 0));
                break;

            case FaceExpression.Success:
                yield return LerpToPose(
                    eyeScale: new Vector3(1.2f, 1.2f, 1f),
                    mouthScale: new Vector3(1.3f, 1f, 1f));
                yield return new WaitForSeconds(0.3f);
                break;
        }

        if (autoReturn)
        {
            yield return new WaitForSeconds(holdDuration);
            yield return TransitionToBaseline();
        }

        blinkingPaused = false;
    }

    IEnumerator TransitionToBaseline()
    {
        yield return LerpToPose(
            eyeScale: eyeLeftBaseScale,
            mouthScale: mouthBaseScale,
            eyeRotL: eyeLeftBaseRot,
            eyeRotR: eyeRightBaseRot,
            eyePosOffsetL: Vector3.zero,
            eyePosOffsetR: Vector3.zero,
            mouthPosOffset: Vector3.zero);
    }

    IEnumerator LerpToPose(
        Vector3? eyeScale = null, Vector3? mouthScale = null,
        Vector3? eyeRotL = null, Vector3? eyeRotR = null,
        Vector3? eyePosOffsetL = null, Vector3? eyePosOffsetR = null,
        Vector3? mouthPosOffset = null)
    {
        Vector3 targetEyeScale = eyeScale ?? eyeLeftBaseScale;
        Vector3 targetMouthScale = mouthScale ?? mouthBaseScale;
        Vector3 targetEyeRotL = eyeRotL ?? eyeLeftBaseRot;
        Vector3 targetEyeRotR = eyeRotR ?? eyeRightBaseRot;
        Vector3 targetEyePosL = eyeLeftBasePos + (eyePosOffsetL ?? Vector3.zero);
        Vector3 targetEyePosR = eyeRightBasePos + (eyePosOffsetR ?? Vector3.zero);
        Vector3 targetMouthPos = mouthBasePos + (mouthPosOffset ?? Vector3.zero);

        Vector3 startEyeScaleL = eyeLeft.localScale, startEyeScaleR = eyeRight.localScale;
        Vector3 startMouthScale = mouth.localScale;
        Vector3 startEyeRotL = eyeLeft.localEulerAngles, startEyeRotR = eyeRight.localEulerAngles;
        Vector3 startEyePosL = eyeLeft.anchoredPosition3D, startEyePosR = eyeRight.anchoredPosition3D;
        Vector3 startMouthPos = mouth.anchoredPosition3D;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * transitionSpeed;

            eyeLeft.localScale = Vector3.Lerp(startEyeScaleL, targetEyeScale, t);
            eyeRight.localScale = Vector3.Lerp(startEyeScaleR, targetEyeScale, t);
            mouth.localScale = Vector3.Lerp(startMouthScale, targetMouthScale, t);

           eyeLeft.localEulerAngles = new Vector3(
           Mathf.LerpAngle(startEyeRotL.x, targetEyeRotL.x, t),
           Mathf.LerpAngle(startEyeRotL.y, targetEyeRotL.y, t),
           Mathf.LerpAngle(startEyeRotL.z, targetEyeRotL.z, t)
           );

            eyeRight.localEulerAngles = new Vector3(
            Mathf.LerpAngle(startEyeRotR.x, targetEyeRotR.x, t),
            Mathf.LerpAngle(startEyeRotR.y, targetEyeRotR.y, t),
            Mathf.LerpAngle(startEyeRotR.z, targetEyeRotR.z, t)
           );

            eyeLeft.anchoredPosition3D = Vector3.Lerp(startEyePosL, targetEyePosL, t);
            eyeRight.anchoredPosition3D = Vector3.Lerp(startEyePosR, targetEyePosR, t);
            mouth.anchoredPosition3D = Vector3.Lerp(startMouthPos, targetMouthPos, t);

            yield return null;
        }
    }

    //Audio talking
    public void StartTalking(AudioClip clip)
    {
        if (talkingRoutine != null) StopCoroutine(talkingRoutine);
        mouth.gameObject.SetActive(false);       // hide smile curve
        mouthTalk.gameObject.SetActive(true);    // show talk oval

        audioSource.clip = clip;
        audioSource.Play();
        talkingRoutine = StartCoroutine(TalkLoop());
    }

    public void StopTalking()
    {
        if (talkingRoutine != null) StopCoroutine(talkingRoutine);
        audioSource.Stop();
        mouthTalk.gameObject.SetActive(false);   // hide talk oval
        mouth.gameObject.SetActive(true);        // show smile curve again
        StartCoroutine(LerpToPose(mouthScale: mouthBaseScale)); // close mouth back to idle
    }

    IEnumerator TalkLoop()
{
    float currentScale = minMouthOpenScale;

    while (audioSource.isPlaying)
    {
        audioSource.GetSpectrumData(audioSampleData, 0, FFTWindow.BlackmanHarris);

        float loudness = 0f;
        for (int i = 0; i < 20; i++)
            loudness += audioSampleData[i];

        float targetScale = Mathf.Clamp(minMouthOpenScale + (loudness * mouthOpenMultiplier * 50f), minMouthOpenScale, maxMouthOpenScale);
        currentScale = Mathf.Lerp(currentScale, targetScale, Time.deltaTime * talkSampleSmoothing);

        mouthTalk.localScale = new Vector3(1f, currentScale, 1f);
        Debug.Log($"Active: {mouthTalk.gameObject.activeSelf} | Scale: {mouthTalk.localScale} | Alpha: {mouthTalk.GetComponent<UnityEngine.UI.Image>().color.a} | Pos: {mouthTalk.anchoredPosition}");

        yield return null;
    }

        mouthTalk.gameObject.SetActive(false);
        mouth.gameObject.SetActive(true);
    }      
}