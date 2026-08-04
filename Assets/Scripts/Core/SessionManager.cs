using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class SessionManager : MonoBehaviour
{
    public event Action OnRecordingFailed;
    public event Action OnReadyToSpeak;
    public event Action<string> OnRobotSpeaking; // fires with the text the robot is about to say
    public event Action OnRobotFinishedSpeaking; // fires when that audio actually finishes
    public static SessionManager Instance;

    public FaceDetectionService detectionService;
    public FaceExpressionController face;

    [Header("Speak Cue")]
    public AudioClip readyToSpeakChime;
    public AudioSource cueAudioSource;
    public float preListenBuffer = 0.4f;

    public string CurrentSessionId { get; private set; }

    public event Action<SessionResponse> OnSessionUpdate;

    private Dictionary<string, AudioClip> audioCache = new Dictionary<string, AudioClip>();

    // --- Cancellation support ---
    private Coroutine activeRecordRoutine;
    private Coroutine activeSendRoutine;

    void Awake() { Instance = this; }

    public void BeginSession(string sessionId)
    {
        CurrentSessionId = sessionId;
    }

    // Call this whenever it's the visitor's turn to speak
    public void RecordAndSend()
    {
        // Make sure any previous recording/send cycle is fully stopped first
        CancelPendingRecording();
        activeRecordRoutine = StartCoroutine(RecordAndSendRoutine());
    }

    // Call this whenever the flow moves away from a state that expects audio
    // (e.g. leaving ConversationScreen), so a late mic result can't get sent
    // to a session that's no longer expecting it.
    public void CancelPendingRecording()
    {
        if (activeRecordRoutine != null)
        {
            StopCoroutine(activeRecordRoutine);
            activeRecordRoutine = null;
        }

        if (activeSendRoutine != null)
        {
            StopCoroutine(activeSendRoutine);
            activeSendRoutine = null;
        }

        if (AudioRecorder.Instance != null)
            AudioRecorder.Instance.StopRecording(); // no-op if nothing is recording; add this method if it doesn't exist yet

        if (listeningIndicatorActive)
            OnRecordingCancelledCleanup();
    }

    private bool listeningIndicatorActive = false;

    private void OnRecordingCancelledCleanup()
    {
        listeningIndicatorActive = false;
        // Intentionally does NOT invoke OnRecordingFailed or any face expression —
        // cancellation is a normal flow transition, not a failure.
    }

    private IEnumerator RecordAndSendRoutine()
    {
        yield return new WaitForSeconds(preListenBuffer);

        if (cueAudioSource != null && readyToSpeakChime != null)
            cueAudioSource.PlayOneShot(readyToSpeakChime);

        listeningIndicatorActive = true;
        OnReadyToSpeak?.Invoke();

        AudioRecorder.Instance.StartRecording(OnAudioRecorded);
    }

    private void OnAudioRecorded(byte[] wavBytes)
    {
        listeningIndicatorActive = false;
        activeRecordRoutine = null;

        if (wavBytes == null)
        {
            Debug.LogWarning("No audio recorded.");
            OnRecordingFailed?.Invoke();
            return;
        }
        activeSendRoutine = StartCoroutine(SendToRespond(wavBytes));
    }

    private IEnumerator SendToRespond(byte[] wavBytes)
    {
        WWWForm form = new WWWForm();
        form.AddField("session_id", CurrentSessionId);
        form.AddBinaryData("audio", wavBytes, "response.wav", "audio/wav");

        using (UnityWebRequest www = UnityWebRequest.Post($"{detectionService.baseUrl}/session/respond", form))
        {
            yield return www.SendWebRequest();

            activeSendRoutine = null;

            if (www.result == UnityWebRequest.Result.Success)
            {
                SessionResponse response = JsonUtility.FromJson<SessionResponse>(www.downloadHandler.text);
                Debug.Log($"Session state: {response.state}, heard: {response.heard_text}");
                yield return PlayResponseAudio(response);
                OnSessionUpdate?.Invoke(response);
            }
            else
            {
                Debug.LogWarning("session/respond failed: " + www.error);
                Debug.LogWarning("Response body: " + www.downloadHandler.text);
                OnRecordingFailed?.Invoke();
            }
        }
    }

    public IEnumerator PlayResponseAudio(SessionResponse response)
    {
        if (!string.IsNullOrEmpty(response.audio_base64))
        {
            byte[] bytes = Convert.FromBase64String(response.audio_base64);
            yield return PlayBytes(bytes, response.answer_text);
        }
        else if (!string.IsNullOrEmpty(response.audio_key))
        {
            yield return PlayCachedOrFetch(response.audio_key, response.answer_text);
        }
    }

    private IEnumerator PlayBytes(byte[] mp3Bytes, string captionText)
    {
        string tempPath = Application.temporaryCachePath + "/session_temp.mp3";
        System.IO.File.WriteAllBytes(tempPath, mp3Bytes);

        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + tempPath, AudioType.MPEG))
        {
            yield return www.SendWebRequest();
            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                yield return PlayAndWaitForFinish(clip, captionText);
            }
        }
    }

    private IEnumerator PlayCachedOrFetch(string key, string captionText)
    {
        if (audioCache.TryGetValue(key, out AudioClip cached))
        {
            yield return PlayAndWaitForFinish(cached, captionText);
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
                yield return PlayAndWaitForFinish(clip, captionText);
            }
        }
    }

    private IEnumerator PlayAndWaitForFinish(AudioClip clip, string captionText = null)
    {
        if (!string.IsNullOrEmpty(captionText))
            OnRobotSpeaking?.Invoke(captionText);

        bool finished = false;
        Action handler = () => finished = true;
        face.OnTalkingFinished += handler;

        face.StartTalking(clip);
        yield return new WaitUntil(() => finished);

        face.OnTalkingFinished -= handler;
        OnRobotFinishedSpeaking?.Invoke();
    }

    // ---------- Backend action calls ----------

    public void SelectHost(int employeeId)
    {
        StartCoroutine(PostJson("/session/confirm-host", $"{{\"session_id\":\"{CurrentSessionId}\",\"employee_id\":{employeeId}}}"));
    }

    public void SubmitName(string name)
    {
        string safeName = name.Replace("\"", "\\\"");
        StartCoroutine(PostJson("/session/submit-name", $"{{\"session_id\":\"{CurrentSessionId}\",\"name\":\"{safeName}\"}}"));
    }

    private IEnumerator PostJson(string endpoint, string json)
    {
        using (UnityWebRequest www = new UnityWebRequest($"{detectionService.baseUrl}{endpoint}", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                SessionResponse response = JsonUtility.FromJson<SessionResponse>(www.downloadHandler.text);
                yield return PlayResponseAudio(response);
                OnSessionUpdate?.Invoke(response);
            }
            else
            {
                Debug.LogWarning($"{endpoint} failed: " + www.error);
                Debug.LogWarning("Response body: " + www.downloadHandler.text);
                OnRecordingFailed?.Invoke();
            }
        }
    }
}