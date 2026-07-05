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
        expressionRoutine = StartCoroutine(TransitionToBaseline());
        currentExpression = FaceExpression.Idle;
    }

    // ---------- BLINK ----------

    IEnumerator BlinkLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minBlinkInterval, maxBlinkInterval));

            if (!blinkingPaused && currentExpression != FaceExpression.Listening)
                yield return Blink();
        }
    }

    IEnumerator Blink()
    {
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
                yield return LerpToPose(
                    eyeRotL: new Vector3(0, 0, -9f),
                    eyeRotR: new Vector3(0, 0, 9f),
                    eyePosOffsetL: new Vector3(0, -8f, 0),
                    eyePosOffsetR: new Vector3(0, -8f, 0),
                    mouthScale: new Vector3(1f, 0.6f, 1f),
                    mouthPosOffset: new Vector3(0, -6f, 0));
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

            eyeLeft.localEulerAngles = Vector3.Lerp(startEyeRotL, targetEyeRotL, t);
            eyeRight.localEulerAngles = Vector3.Lerp(startEyeRotR, targetEyeRotR, t);

            eyeLeft.anchoredPosition3D = Vector3.Lerp(startEyePosL, targetEyePosL, t);
            eyeRight.anchoredPosition3D = Vector3.Lerp(startEyePosR, targetEyePosR, t);
            mouth.anchoredPosition3D = Vector3.Lerp(startMouthPos, targetMouthPos, t);

            yield return null;
        }
    }
}