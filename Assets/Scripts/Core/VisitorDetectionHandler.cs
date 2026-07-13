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

                detectionService.StopPolling();
                face.SetExpression(FaceExpression.Happy);
                StartCoroutine(PlayCachedOrFetchAudio(result.audio_key));
                flowManager.GoTo(VisitorFlowState.CollectName);
                break;

            case "known":
                if (result.session_id == lastSessionId) return;
                lastSessionId = result.session_id;

                detectionService.StopPolling();
                face.SetExpression(FaceExpression.Happy);
                PlayBase64Audio(result.audio_base64);

                if (flowManager.Session != null)
                {
                    flowManager.Session.visitorName = result.visitor_name;
                }

                flowManager.GoTo(VisitorFlowState.GreetKnownVisitor);
                StartCoroutine(AdvanceToAskPurposeAfterGreeting());
                break;
        }
    } // <-- THIS was missing — closes HandleResult()

    // ---------- KNOWN: inline base64 audio ----------

    private void PlayBase64Audio(string base64)
    {
        if (string.IsNullOrEmpty(base64)) return;

        byte[] audioBytes = System.Convert.FromBase64String(base64);
        StartCoroutine(PlayAudioBytes(audioBytes));
    }

    private IEnumerator PlayAudioBytes(byte[] mp3Bytes)
    {
        string tempPath = Application.temporaryCachePath + "/greeting_temp.mp3";
        System.IO.File.WriteAllBytes(tempPath, mp3Bytes);

        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + tempPath, AudioType.MPEG))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                face.StartTalking(clip);
            }
            else
            {
                Debug.LogWarning("Failed to decode greeting audio: " + www.error);
            }
        }
    }

    // ---------- KNOWN: advance to AskPurpose after greeting ----------

    private IEnumerator AdvanceToAskPurposeAfterGreeting()
    {
        yield return new WaitForSeconds(3f); // gives the greeting audio time to play — tune as needed
        flowManager.GoTo(VisitorFlowState.AskPurpose);
    }

    // ---------- UNKNOWN: fetch + cache from /audio/{key} ----------

    private IEnumerator PlayCachedOrFetchAudio(string key)
    {
        if (string.IsNullOrEmpty(key)) yield break;

        if (audioCache.TryGetValue(key, out AudioClip cached))
        {
            face.StartTalking(cached);
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
                face.StartTalking(clip);
            }
            else
            {
                Debug.LogWarning("Failed to fetch audio for key '" + key + "': " + www.error);
            }
        }
    }
}