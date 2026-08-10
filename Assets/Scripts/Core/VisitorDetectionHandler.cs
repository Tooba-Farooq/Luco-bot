using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class VisitorDetectionHandler : MonoBehaviour
{
    public FaceDetectionService detectionService;
    public FaceExpressionController face;
    public VisitorFlowManager flowManager;

    private string lastStatus = "";
    private string lastSessionId = "";
    private Dictionary<string, AudioClip> audioCache = new Dictionary<string, AudioClip>();

    [Header("UI")]
    public CaptionBarController captionBar;
    public string idlePromptText = "Come closer to chat";
    public string detectingPromptText = "Keep looking at me...";
    void OnEnable()
    {
        detectionService.OnDetectionResult += HandleResult;
    }

    void OnDisable()
    {
        detectionService.OnDetectionResult -= HandleResult;
    }

    public void ResetDetectionState()
    {
        lastStatus = "";
        lastSessionId = "";
    }

    void HandleResult(DetectResponse result)
    {
        Debug.Log($"Detect result: status={result.status}, face_forward={result.face_forward}, duration={result.forward_duration}");

        if (result.status == lastStatus) return;
        lastStatus = result.status;

        bool flowIsIdle = flowManager.CurrentState == VisitorFlowState.Idle
                        || flowManager.CurrentState == VisitorFlowState.DetectingPerson;

        if (!flowIsIdle) return;

        switch (result.status)
        {
            case "idle":
                face.ReturnToIdle();
                flowManager.GoTo(VisitorFlowState.Idle);
                ShowCaption(idlePromptText);
                break;

            case "detecting":
                ShowCaption(detectingPromptText);
                flowManager.GoTo(VisitorFlowState.DetectingPerson);
                break;

            case "unknown":
                if (result.session_id == lastSessionId) return;
                lastSessionId = result.session_id;
                HideCaption();
                SessionManager.Instance.BeginSession(result.session_id);
                flowManager.Session.isKnownVisitor = false;
                detectionService.StopPolling();
                if (face != null) face.SetExpression(FaceExpression.Greeting, autoReturnToIdle: false);
                StartCoroutine(PlayCachedOrFetchAudio(result.audio_key, result.answer_text));
                break;

            case "known":
                if (result.session_id == lastSessionId) return;
                lastSessionId = result.session_id;
                HideCaption();
                SessionManager.Instance.BeginSession(result.session_id);
                flowManager.Session.isKnownVisitor = true;
                flowManager.Session.visitorName = result.visitor_name;
                detectionService.StopPolling();
                if (face != null) face.SetExpression(FaceExpression.Greeting, autoReturnToIdle: false);
                PlayBase64Audio(result.audio_base64, result.answer_text);
                break;
        }
    }

    // ---------- KNOWN: inline base64 audio ----------

    private void PlayBase64Audio(string base64, string captionText)
    {
        if (string.IsNullOrEmpty(base64)) return;

        byte[] audioBytes = System.Convert.FromBase64String(base64);
        StartCoroutine(PlayAudioBytesAndAdvance(audioBytes, captionText));
    }

    private IEnumerator PlayAudioBytesAndAdvance(byte[] mp3Bytes, string captionText)
    {
        string tempPath = Application.temporaryCachePath + "/greeting_temp.mp3";
        System.IO.File.WriteAllBytes(tempPath, mp3Bytes);

        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + tempPath, AudioType.MPEG))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                ShowCaption(captionText, clip.length);
                yield return PlayAndAdvance(clip);
            }
            else
            {
                Debug.LogWarning("Failed to decode greeting audio: " + www.error);
                yield return AdvanceAfterGreeting();
            }
        }
    }

    // ---------- UNKNOWN: fetch + cache from /audio/{key} ----------

    private IEnumerator PlayCachedOrFetchAudio(string key, string captionText)
    {
        if (string.IsNullOrEmpty(key))
        {
            yield return AdvanceAfterGreeting();
            yield break;
        }

        if (audioCache.TryGetValue(key, out AudioClip cached))
        {
            ShowCaption(captionText, cached.length);
            yield return PlayAndAdvance(cached);
            yield break;
        }

        string url = $"{detectionService.baseUrl}/audio/{key}";
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                audioCache[key] = clip;
                ShowCaption(captionText, clip.length);
                yield return PlayAndAdvance(clip);
            }
            else
            {
                Debug.LogWarning("Failed to fetch audio for key '" + key + "': " + www.error);
                yield return AdvanceAfterGreeting();
            }
        }
    }

    // ---------- Caption show/hide ----------

    private void ShowCaption(string text, float syncDuration = -1f)
    {
        if (captionBar != null)
            captionBar.ShowCaption(text, syncDuration);
    }

    private void HideCaption()
    {
        if (captionBar != null)
            captionBar.HideCaption();
    }

    // ---------- Shared: play clip, wait for REAL finish, then advance ----------

    private IEnumerator PlayAndAdvance(AudioClip clip)
    {
        bool finished = false;
        System.Action handler = () => finished = true;
        face.OnTalkingFinished += handler;

        face.StartTalking(clip);
        yield return new WaitUntil(() => finished);

        face.OnTalkingFinished -= handler;

        yield return AdvanceAfterGreeting();
    }

    private IEnumerator AdvanceAfterGreeting()
    {
        HideCaption();
        flowManager.GoTo(VisitorFlowState.MeetSomeone_EnterHostName);
        yield break;
    }
}