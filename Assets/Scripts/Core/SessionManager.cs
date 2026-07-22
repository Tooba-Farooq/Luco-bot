using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class SessionManager : MonoBehaviour
{
    public event Action OnRecordingFailed;
    public static SessionManager Instance;

    public FaceDetectionService detectionService;
    public FaceExpressionController face;

    public string CurrentSessionId { get; private set; }

    public event Action<SessionResponse> OnSessionUpdate;

    private Dictionary<string, AudioClip> audioCache = new Dictionary<string, AudioClip>();

    void Awake() { Instance = this; }

    public void BeginSession(string sessionId)
    {
        CurrentSessionId = sessionId;
    }

    // Call this whenever it's the visitor's turn to speak
    public void RecordAndSend()
    {
        AudioRecorder.Instance.StartRecording(OnAudioRecorded);
    }

    private void OnAudioRecorded(byte[] wavBytes)
    {
        if (wavBytes == null)
        {
            Debug.LogWarning("No audio recorded.");
            OnRecordingFailed?.Invoke();
            return;
        }
        StartCoroutine(SendToRespond(wavBytes));
    }

    private IEnumerator SendToRespond(byte[] wavBytes)
    {
        WWWForm form = new WWWForm();
        form.AddField("session_id", CurrentSessionId);
        form.AddBinaryData("audio", wavBytes, "response.wav", "audio/wav");

        using (UnityWebRequest www = UnityWebRequest.Post($"{detectionService.baseUrl}/session/respond", form))
        {
            yield return www.SendWebRequest();

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
                Debug.LogWarning("Response body: " + www.downloadHandler.text); // ADD — shows the real reason
                OnRecordingFailed?.Invoke();
            }
        }
    }

    public IEnumerator PlayResponseAudio(SessionResponse response)
    {
        if (!string.IsNullOrEmpty(response.audio_base64))
        {
            byte[] bytes = Convert.FromBase64String(response.audio_base64);
            yield return PlayBytes(bytes);
        }
        else if (!string.IsNullOrEmpty(response.audio_key))
        {
            yield return PlayCachedOrFetch(response.audio_key);
        }
    }

    private IEnumerator PlayBytes(byte[] mp3Bytes)
    {
        string tempPath = Application.temporaryCachePath + "/session_temp.mp3";
        System.IO.File.WriteAllBytes(tempPath, mp3Bytes);

        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + tempPath, AudioType.MPEG))
        {
            yield return www.SendWebRequest();
            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                face.StartTalking(clip);
                yield return new WaitForSeconds(clip.length + 0.2f);
            }
        }
    }

    private IEnumerator PlayCachedOrFetch(string key)
    {
        if (audioCache.TryGetValue(key, out AudioClip cached))
        {
            face.StartTalking(cached);
            yield return new WaitForSeconds(cached.length + 0.2f);
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
                yield return new WaitForSeconds(clip.length + 0.2f);
            }
        }
    }

    // ---------- Backend action calls ----------

    public void SelectHost(int employeeId)
    {
        StartCoroutine(PostJson("/session/confirm-host", $"{{\"session_id\":\"{CurrentSessionId}\",\"employee_id\":{employeeId}}}"));
    }

    //public void RetryHostName()
    //{
   //     StartCoroutine(PostJson("/session/retry-host-name", $"{{\"session_id\":\"{CurrentSessionId}\"}}"));
    //}

    public void SubmitName(string name)
    {
        string safeName = name.Replace("\"", "\\\""); // basic JSON-safety for quotes in the name
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
            }
        }
    }
}