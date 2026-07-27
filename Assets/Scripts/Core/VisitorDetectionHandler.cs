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
    public GameObject greetingCaptionBubble;   // the Image parent
    public TMPro.TMP_Text greetingCaptionText; // the TMP child

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

        if (!flowIsIdle) return;

        switch (result.status)
        {
            case "idle":
                face.ReturnToIdle();
                flowManager.GoTo(VisitorFlowState.Idle);
                break;

            case "detecting":
                flowManager.GoTo(VisitorFlowState.DetectingPerson);
                break;

            case "unknown":
                if (result.session_id == lastSessionId) return;
                lastSessionId = result.session_id;
                SessionManager.Instance.BeginSession(result.session_id);
                flowManager.Session.isKnownVisitor = false;
                detectionService.StopPolling();
                StartCoroutine(PlayCachedOrFetchAudio(result.audio_key, result.answer_text)); // UPDATED
                break;

            case "known":
                if (result.session_id == lastSessionId) return;
                lastSessionId = result.session_id;
                SessionManager.Instance.BeginSession(result.session_id);
                flowManager.Session.isKnownVisitor = true;
                flowManager.Session.visitorName = result.visitor_name;
                detectionService.StopPolling();
                PlayBase64Audio(result.audio_base64, result.answer_text); // UPDATED
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
                ShowCaption(captionText);
                face.StartTalking(clip);
                yield return AdvanceAfterGreeting(clip);
            }
            else
            {
                Debug.LogWarning("Failed to decode greeting audio: " + www.error);
                yield return AdvanceAfterGreeting(null);
            }
        }
    }

    // ---------- UNKNOWN: fetch + cache from /audio/{key} ----------

    private IEnumerator PlayCachedOrFetchAudio(string key, string captionText)
    {
        if (string.IsNullOrEmpty(key))
        {
            yield return AdvanceAfterGreeting(null);
            yield break;
        }

        if (audioCache.TryGetValue(key, out AudioClip cached))
        {
            ShowCaption(captionText);
            face.StartTalking(cached);
            yield return AdvanceAfterGreeting(cached);
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
                ShowCaption(captionText);
                face.StartTalking(clip);
                yield return AdvanceAfterGreeting(clip);
            }
            else
            {
                Debug.LogWarning("Failed to fetch audio for key '" + key + "': " + www.error);
                yield return AdvanceAfterGreeting(null);
            }
        }
    }

    // ---------- Caption show/hide ----------

    private void ShowCaption(string text)
    {
        if (greetingCaptionBubble == null || greetingCaptionText == null) return;
        greetingCaptionText.text = text;
        greetingCaptionBubble.SetActive(true);
    }

    private void HideCaption()
    {
        if (greetingCaptionBubble == null) return;
        greetingCaptionBubble.SetActive(false);
    }
    //----------reset status and session id when idle----------
    public void ResetDetectionState()
    {
        lastStatus = "";
        lastSessionId = "";
    }

    // ---------- Shared advance logic ----------

    private IEnumerator AdvanceAfterGreeting(AudioClip clip)
    {
        float waitTime = (clip != null) ? clip.length + 0.3f : 2f;
        yield return new WaitForSeconds(waitTime);
        HideCaption();
        flowManager.GoTo(VisitorFlowState.MeetSomeone_EnterHostName);
    }
}