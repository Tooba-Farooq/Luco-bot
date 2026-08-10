using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class SessionManager : MonoBehaviour
{
    public event Action OnRecordingFailed;
    public event Action OnReadyToSpeak;
    public event Action<string, float> OnRobotSpeaking;
    public event Action OnRobotFinishedSpeaking;

    public static SessionManager Instance;

    public FaceDetectionService detectionService;
    public FaceExpressionController face;

    [Header("Speak Cue")]
    public AudioClip readyToSpeakChime;
    public AudioSource cueAudioSource;
    public float preListenBuffer = 0.4f;

    [Header("Speak Now Prompt")]
    public string speakNowText = "Speak now";
    public float speakNowDisplayDuration = 1.2f; // how long the text stays up before listening starts

    public string CurrentSessionId { get; private set; }

    public event Action<SessionResponse> OnSessionUpdate;

    private Dictionary<string, AudioClip> audioCache =
        new Dictionary<string, AudioClip>();

    // Cancellation support
    private Coroutine activeRecordRoutine;
    private Coroutine activeSendRoutine;
    private Coroutine activeResponseAudioRoutine;
    private System.Action activeTalkingFinishedHandler;

    void Awake()
    {
        Instance = this;
    }

    public void BeginSession(string sessionId)
    {
        CurrentSessionId = sessionId;
    }

    // =========================================================
    // RECORDING
    // =========================================================

    public void RecordAndSend()
    {
        CancelPendingRecording();

        activeRecordRoutine =
            StartCoroutine(RecordAndSendRoutine());
    }

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
        {
            AudioRecorder.Instance.StopRecording();
        }

        if (listeningIndicatorActive)
        {
            OnRecordingCancelledCleanup();
        }
    }

    private bool listeningIndicatorActive = false;

    private void OnRecordingCancelledCleanup()
    {
        listeningIndicatorActive = false;
    }

    private IEnumerator RecordAndSendRoutine()
{
    yield return new WaitForSeconds(preListenBuffer);

    if (!string.IsNullOrEmpty(speakNowText))
    {
        OnRobotSpeaking?.Invoke(speakNowText, speakNowDisplayDuration);
        yield return new WaitForSeconds(speakNowDisplayDuration);
    }

    listeningIndicatorActive = true;

    OnReadyToSpeak?.Invoke();

    if (AudioRecorder.Instance != null)
    {
        AudioRecorder.Instance.StartRecording(OnAudioRecorded);
    }
    else
    {
        Debug.LogError("[SessionManager] AudioRecorder.Instance is NULL.");
    }
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

        activeSendRoutine =
            StartCoroutine(
                SendToRespond(wavBytes)
            );
    }

    // =========================================================
    // NORMAL VOICE RESPONSE
    // =========================================================

    private IEnumerator SendToRespond(byte[] wavBytes)
    {
        WWWForm form = new WWWForm();

        form.AddField(
            "session_id",
            CurrentSessionId
        );

        form.AddBinaryData(
            "audio",
            wavBytes,
            "response.wav",
            "audio/wav"
        );

        using (UnityWebRequest www =
               UnityWebRequest.Post(
                   $"{detectionService.baseUrl}/session/respond",
                   form))
        {
            yield return www.SendWebRequest();

            activeSendRoutine = null;

            if (www.result ==
                UnityWebRequest.Result.Success)
            {
                SessionResponse response =
                    JsonUtility.FromJson<SessionResponse>(
                        www.downloadHandler.text
                    );

                Debug.Log(
                    $"[SESSION RESPONSE] " +
                    $"state={response.state} | " +
                    $"heard='{response.heard_text}' | " +
                    $"answer='{response.answer_text}'"
                );

                // IMPORTANT:
                // The robot speaks FIRST.
                //
                // We wait until the audio has completely finished
                // before notifying ConversationScreen.
                yield return PlayResponseAudio(response);

                // Only now does ConversationScreen receive the state.
                OnSessionUpdate?.Invoke(response);
            }
            else
            {
                Debug.LogWarning(
                    "session/respond failed: " +
                    www.error
                );

                Debug.LogWarning(
                    "Response body: " +
                    www.downloadHandler.text
                );

                OnRecordingFailed?.Invoke();
            }
        }
    }

    // =========================================================
    // RESPONSE AUDIO
    // =========================================================

    public IEnumerator PlayResponseAudio(
        SessionResponse response)
    {
        if (response == null)
        {
            yield break;
        }

        if (!string.IsNullOrEmpty(
                response.audio_base64))
        {
            byte[] bytes =
                Convert.FromBase64String(
                    response.audio_base64
                );

            yield return PlayBytes(
                bytes,
                response.answer_text
            );
        }
        else if (!string.IsNullOrEmpty(
                     response.audio_key))
        {
            yield return PlayCachedOrFetch(
                response.audio_key,
                response.answer_text
            );
        }
        else
        {
            Debug.Log(
                "[SessionManager] Response has no audio."
            );
        }
    }

    // =========================================================
    // AUDIO PLAYBACK
    // =========================================================

    private IEnumerator PlayBytes(
        byte[] mp3Bytes,
        string captionText)
    {
        string tempPath =
            Application.temporaryCachePath +
            "/session_temp.mp3";

        System.IO.File.WriteAllBytes(
            tempPath,
            mp3Bytes
        );

        using (UnityWebRequest www =
               UnityWebRequestMultimedia.GetAudioClip(
                   "file://" + tempPath,
                   AudioType.MPEG))
        {
            yield return www.SendWebRequest();

            if (www.result ==
                UnityWebRequest.Result.Success)
            {
                AudioClip clip =
                    DownloadHandlerAudioClip.GetContent(
                        www
                    );

                yield return PlayAndWaitForFinish(
                    clip,
                    captionText
                );
            }
            else
            {
                Debug.LogWarning(
                    "[SessionManager] Failed to load response audio: " +
                    www.error
                );
            }
        }
    }

    private IEnumerator PlayCachedOrFetch(
        string key,
        string captionText)
    {
        if (audioCache.TryGetValue(
                key,
                out AudioClip cached))
        {
            yield return PlayAndWaitForFinish(
                cached,
                captionText
            );

            yield break;
        }

        string url =
            $"{detectionService.baseUrl}/audio/{key}";

        using (UnityWebRequest www =
               UnityWebRequestMultimedia.GetAudioClip(
                   url,
                   AudioType.MPEG))
        {
            yield return www.SendWebRequest();

            if (www.result ==
                UnityWebRequest.Result.Success)
            {
                AudioClip clip =
                    DownloadHandlerAudioClip.GetContent(
                        www
                    );

                audioCache[key] = clip;

                yield return PlayAndWaitForFinish(
                    clip,
                    captionText
                );
            }
            else
            {
                Debug.LogWarning(
                    "[SessionManager] Failed to fetch audio: " +
                    www.error
                );
            }
        }
    }

    private IEnumerator PlayAndWaitForFinish(
        AudioClip clip,
        string captionText = null)
    {
        if (clip == null)
        {
            Debug.LogWarning(
                "[SessionManager] AudioClip is NULL."
            );

            yield break;
        }

        if (!string.IsNullOrEmpty(captionText))
        {
            OnRobotSpeaking?.Invoke(captionText, clip.length);
        }

        bool finished = false;

        activeTalkingFinishedHandler =
            () => finished = true;

        if (face != null)
        {
            face.OnTalkingFinished +=
                activeTalkingFinishedHandler;

            face.StartTalking(clip);
        }
        else
        {
            Debug.LogError(
                "[SessionManager] FaceExpressionController is NULL."
            );

            yield break;
        }

        yield return new WaitUntil(
            () => finished
        );

        if (face != null)
        {
            face.OnTalkingFinished -=
                activeTalkingFinishedHandler;
        }

        activeTalkingFinishedHandler = null;

        OnRobotFinishedSpeaking?.Invoke();
    }

    // =========================================================
    // BACKEND ACTION CALLS
    // =========================================================

    public void SelectHost(int employeeId)
    {
        StartCoroutine(
            PostJson(
                "/session/confirm-host",
                $"{{\"session_id\":\"{CurrentSessionId}\",\"employee_id\":{employeeId}}}"
            )
        );
    }

    public void SubmitName(string name)
    {
        string safeName =
            name.Replace("\"", "\\\"");

        StartCoroutine(
            PostJson(
                "/session/submit-name",
                $"{{\"session_id\":\"{CurrentSessionId}\",\"name\":\"{safeName}\"}}"
            )
        );
    }

    // =========================================================
    // BUTTON / JSON RESPONSES
    // =========================================================

    private IEnumerator PostJson(
        string endpoint,
        string json)
    {
        using (UnityWebRequest www =
               new UnityWebRequest(
                   $"{detectionService.baseUrl}{endpoint}",
                   "POST"))
        {
            byte[] bodyRaw =
                System.Text.Encoding.UTF8.GetBytes(
                    json
                );

            www.uploadHandler =
                new UploadHandlerRaw(bodyRaw);

            www.downloadHandler =
                new DownloadHandlerBuffer();

            www.SetRequestHeader(
                "Content-Type",
                "application/json"
            );

            yield return www.SendWebRequest();

            if (www.result ==
                UnityWebRequest.Result.Success)
            {
                SessionResponse response =
                    JsonUtility.FromJson<SessionResponse>(
                        www.downloadHandler.text
                    );

                Debug.Log(
                    $"[POST RESPONSE] " +
                    $"endpoint={endpoint} | " +
                    $"state={response.state} | " +
                    $"heard='{response.heard_text}' | " +
                    $"answer='{response.answer_text}'"
                );

                // IMPORTANT:
                // Same sequencing as SendToRespond:
                //
                // 1. Play robot response.
                // 2. Wait until robot finishes.
                // 3. Then notify ConversationScreen.
                yield return PlayResponseAudio(response);

                OnSessionUpdate?.Invoke(response);
            }
            else
            {
                Debug.LogWarning(
                    $"{endpoint} failed: " +
                    www.error
                );

                Debug.LogWarning(
                    "Response body: " +
                    www.downloadHandler.text
                );

                OnRecordingFailed?.Invoke();
            }
        }
    }
}

