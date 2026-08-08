using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public enum FaceExpression
{
    Idle,
    Listening,
    Happy,
    Thinking,
    Apologetic,
    Success,
    Greeting,
    Handoff,
    Purpose,
    Confused
}

public class FaceExpressionController : MonoBehaviour
{
    [Header("Face Parts")]
    public RectTransform eyeLeft;
    public RectTransform eyeRight;
    public RectTransform mouth;

    [Header("Face Parts - Sprites")]
    public Image eyeLeftImage;
    public Image eyeRightImage;
    public Image eyebrowLeftImage;
    public Image eyebrowRightImage;
    public Image mouthImage;
    public Image mouthTalkImage;
    public Image blushLeftImage;
    public Image blushRightImage;

    [Header("Eye Sprites")]
    public Sprite eyeOpenSprite;

    [Header("Eyebrow Sprites")]
    public Sprite browNeutralSprite;
    public Sprite browRaisedSprite;
    public Sprite browAngledSprite;

    [Header("Mouth Sprites (static expressions)")]
    public Sprite mouthClosedSprite;
    public Sprite mouthSmileSprite;

    [Header("Mouth Sprites (lip-sync visemes)")]
    public Sprite mouthA;
    public Sprite mouthI;
    public Sprite mouthU;
    public Sprite mouthE;
    public Sprite mouthO;

    [Header("Blink Settings")]
    public float minBlinkInterval = 2f;
    public float maxBlinkInterval = 5f;
    public float blinkSpeed = 12f;

    [Header("Eye Wander Settings")]
    public float minWanderInterval = 3f;
    public float maxWanderInterval = 6f;
    public float wanderOffsetRange = 10f;

    [Header("Expression Transition Speed")]
    public float transitionSpeed = 6f;

    private FaceExpression currentExpression = FaceExpression.Idle;
    private Coroutine expressionRoutine;
    private Coroutine blinkLoopRoutine;
    private Coroutine eyeWanderRoutine;
    private bool blinkingPaused = false;

    // Baseline (idle) values, captured at Start so every expression can return to them
    private Vector3 eyeLeftBaseScale, eyeRightBaseScale, mouthBaseScale;
    private Vector3 eyeLeftBasePos, eyeRightBasePos, mouthBasePos;
    private Vector3 eyeLeftBaseRot, eyeRightBaseRot;
    private Vector3 faceRootBasePos, faceRootBaseScale;

    [Header("Talking")]
    public AudioSource audioSource;
    public AudioClip testClip;
    public RectTransform mouthTalk;

    public event System.Action OnTalkingFinished;

    private Coroutine breathingRoutine;

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

        faceRootBasePos = ((RectTransform)transform).anchoredPosition3D;
        faceRootBaseScale = transform.localScale;

        // Set initial sprite state
        eyeLeftImage.sprite = eyeOpenSprite;
        eyeRightImage.sprite = eyeOpenSprite;
        SetEyebrowSprite(browNeutralSprite);
        mouthImage.sprite = mouthSmileSprite;

        blinkLoopRoutine = StartCoroutine(BlinkLoop());
        breathingRoutine = StartCoroutine(BreathingLoop());
        eyeWanderRoutine = StartCoroutine(EyeWanderLoop());
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

    // ---------- EYEBROW HELPER ----------

    void SetEyebrowSprite(Sprite s)
    {
        eyebrowLeftImage.sprite = s;
        eyebrowRightImage.sprite = s;
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
        Vector3 closedR = new Vector3(openR.x, 0.1f, openR.z); // openR.x already carries the -1 flip

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * blinkSpeed;
            eyeLeft.localScale = Vector3.Lerp(openL, closedL, t);
            eyeRight.localScale = Vector3.Lerp(openR, closedR, t);
            yield return null;
        }
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * blinkSpeed;
            eyeLeft.localScale = Vector3.Lerp(closedL, openL, t);
            eyeRight.localScale = Vector3.Lerp(closedR, openR, t);
            yield return null;
        }
    }

    IEnumerator BreathingLoop()
    {
        float t = 0f;
        while (true)
        {
            if (currentExpression == FaceExpression.Idle && !blinkingPaused)
            {
                t += Time.deltaTime * 0.8f;
                float scale = 1f + Mathf.Sin(t) * 0.02f;
                transform.localScale = faceRootBaseScale * scale;
            }
            yield return null;
        }
    }

    // ---------- EYE WANDER ----------

    IEnumerator EyeWanderLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minWanderInterval, maxWanderInterval));

            if (currentExpression == FaceExpression.Idle && !blinkingPaused)
            {
                float offset = Random.Range(-wanderOffsetRange, wanderOffsetRange);
                yield return EyeLook(xOffset: offset, duration: 0.4f);
            }
        }
    }

    // ---------- BOUNCE ----------

    IEnumerator Bounce(float height = 15f, float duration = 0.5f)
    {
        RectTransform rootRT = (RectTransform)transform;
        Vector3 startPos = faceRootBasePos;
        Vector3 upPos = startPos + new Vector3(0, height, 0);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / (duration * 0.5f);
            rootRT.anchoredPosition3D = Vector3.Lerp(startPos, upPos, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / (duration * 0.5f);
            rootRT.anchoredPosition3D = Vector3.Lerp(upPos, startPos, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        rootRT.anchoredPosition3D = startPos;
    }

    // ---------- EYE LOOK ----------

    IEnumerator EyeLook(float xOffset = 12f, float duration = 0.25f)
    {
        Vector3 startL = eyeLeft.anchoredPosition3D;
        Vector3 startR = eyeRight.anchoredPosition3D;
        Vector3 targetL = eyeLeftBasePos + new Vector3(xOffset, 0, 0);
        Vector3 targetR = eyeRightBasePos + new Vector3(xOffset, 0, 0);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            eyeLeft.anchoredPosition3D = Vector3.Lerp(startL, targetL, t);
            eyeRight.anchoredPosition3D = Vector3.Lerp(startR, targetR, t);
            yield return null;
        }

        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            eyeLeft.anchoredPosition3D = Vector3.Lerp(targetL, eyeLeftBasePos, t);
            eyeRight.anchoredPosition3D = Vector3.Lerp(targetR, eyeRightBasePos, t);
            yield return null;
        }
    }

    // ---------- HEAD TILT ----------

    IEnumerator HeadTilt(float angle = 12f, float duration = 0.3f)
    {
        Vector3 startL = eyeLeft.localEulerAngles;
        Vector3 startR = eyeRight.localEulerAngles;
        Vector3 targetL = eyeLeftBaseRot + new Vector3(0, 0, angle);
        Vector3 targetR = eyeRightBaseRot + new Vector3(0, 0, angle);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            eyeLeft.localEulerAngles = new Vector3(
                Mathf.LerpAngle(startL.x, targetL.x, t),
                Mathf.LerpAngle(startL.y, targetL.y, t),
                Mathf.LerpAngle(startL.z, targetL.z, t));
            eyeRight.localEulerAngles = new Vector3(
                Mathf.LerpAngle(startR.x, targetR.x, t),
                Mathf.LerpAngle(startR.y, targetR.y, t),
                Mathf.LerpAngle(startR.z, targetR.z, t));
            yield return null;
        }
    }

    // ---------- EXPRESSIONS ----------

    IEnumerator PlayExpression(FaceExpression expression, bool autoReturn, float holdDuration)
    {
        blinkingPaused = true;

        switch (expression)
        {
            case FaceExpression.Happy:
                SetEyebrowSprite(browRaisedSprite);
                mouthImage.sprite = mouthSmileSprite;
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
                SetEyebrowSprite(browAngledSprite);
                float originalSpeed = transitionSpeed;
                transitionSpeed = 3f;
                yield return LerpToPose(
                    eyeScale: new Vector3(1f, 0.5f, 1f),
                    eyePosOffsetL: new Vector3(0, -6f, 0),
                    eyePosOffsetR: new Vector3(0, -6f, 0),
                    mouthScale: new Vector3(1f, -0.8f, 1f),
                    mouthPosOffset: new Vector3(0, -4f, 0));
                transitionSpeed = originalSpeed;
                break;

            case FaceExpression.Success:
                SetEyebrowSprite(browRaisedSprite);
                StartCoroutine(Bounce());
                yield return LerpToPose(
                    eyeScale: new Vector3(1.2f, 1.2f, 1f),
                    mouthScale: new Vector3(1.3f, 1f, 1f));
                yield return new WaitForSeconds(0.3f);
                break;

            case FaceExpression.Greeting:
                SetEyebrowSprite(browRaisedSprite);
                yield return EyeLook();
                StartCoroutine(Bounce());
                yield return LerpToPose(
                    eyeScale: new Vector3(1.2f, 1.2f, 1f),
                    mouthScale: new Vector3(1.2f, 1f, 1f));
                break;

            case FaceExpression.Purpose:
                yield return HeadTilt();
                break;

            case FaceExpression.Confused:
                SetEyebrowSprite(browAngledSprite);
                yield return LerpToPose(
                    eyeScale: new Vector3(0.85f, 0.85f, 1f));
                yield return HeadTilt(angle: -8f, duration: 0.25f);
                yield return new WaitForSeconds(0.5f);
                break;

            case FaceExpression.Handoff:
                SetEyebrowSprite(browRaisedSprite);
                StartCoroutine(Bounce(height: 20f, duration: 0.4f));
                yield return LerpToPose(
                    eyeScale: new Vector3(1.2f, 1.2f, 1f),
                    mouthScale: new Vector3(1.3f, 1f, 1f));
                yield return new WaitForSeconds(0.2f);
                StartCoroutine(Bounce(height: 10f, duration: 0.3f));
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
        SetEyebrowSprite(browNeutralSprite);
        mouthImage.sprite = mouthSmileSprite;

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
        // Left eye target
        Vector3 targetEyeScaleL = eyeScale ?? eyeLeftBaseScale;

        // Right eye target — same magnitude, but preserves the right eye's own
        // baseline sign so a -1 X flip survives expression transitions
        Vector3 targetEyeScaleR;
        if (eyeScale.HasValue)
        {
            targetEyeScaleR = new Vector3(
                Mathf.Abs(eyeScale.Value.x) * Mathf.Sign(eyeRightBaseScale.x),
                eyeScale.Value.y,
                eyeScale.Value.z);
        }
        else
        {
            targetEyeScaleR = eyeRightBaseScale;
        }

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

            eyeLeft.localScale = Vector3.Lerp(startEyeScaleL, targetEyeScaleL, t);
            eyeRight.localScale = Vector3.Lerp(startEyeScaleR, targetEyeScaleR, t);
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

    // ---------- Talking ----------
    // Sprite swapping during speech is now driven entirely by LipSyncMouthReceiver,
    // which listens to uLipSync's "On Lip Sync Update" event. This class just
    // handles showing/hiding the mouthTalk object and starting/stopping playback.

    public void StartTalking(AudioClip clip)
    {
        mouth.gameObject.SetActive(false);
        mouthTalk.gameObject.SetActive(true);

        audioSource.clip = clip;
        audioSource.Play();
        StartCoroutine(WaitForTalkingToFinish());
    }

    public void StopTalking()
    {
        audioSource.Stop();
        mouthTalk.gameObject.SetActive(false);
        mouth.gameObject.SetActive(true);
        mouthImage.sprite = mouthClosedSprite;
        StartCoroutine(LerpToPose(mouthScale: mouthBaseScale));
    }

    IEnumerator WaitForTalkingToFinish()
    {
        while (audioSource.isPlaying)
            yield return null;

        mouthTalk.gameObject.SetActive(false);
        mouth.gameObject.SetActive(true);
        OnTalkingFinished?.Invoke();
    }
}