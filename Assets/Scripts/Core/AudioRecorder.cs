using UnityEngine;
using System;
using System.Collections;
using System.IO;

public class AudioRecorder : MonoBehaviour
{
    public static AudioRecorder Instance;

    [Header("Recording Settings")]
    public int sampleRate = 16000; // Whisper prefers 16kHz
    public float silenceThreshold = 0.02f; // calibrated: silence ~0.001-0.006, well under this
    public float silenceDurationToStop = 1.8f;
    public float maxRecordingLength = 15f;
    public float minRecordingLength = 0.5f; // avoid sending empty/near-empty clips

    [Header("Debug")]
    [Tooltip("Logs live volume readings to the console for threshold calibration.")]
    public bool logVolumeForCalibration = false;

    private string micDevice;
    private AudioClip recordingClip;
    private bool isRecording = false;
    private Coroutine activeRecordCoroutine;
    private Action<byte[]> activeOnComplete;

    void Awake()
    {
        Instance = this;
        if (Microphone.devices.Length > 0)
            micDevice = Microphone.devices[0];
    }

    public void StartRecording(Action<byte[]> onComplete)
    {
        if (string.IsNullOrEmpty(micDevice))
        {
            Debug.LogWarning("No microphone device available.");
            onComplete?.Invoke(null);
            return;
        }

        // Guard against overlapping recordings
        if (isRecording)
        {
            Debug.LogWarning("StartRecording called while already recording — stopping previous recording first.");
            StopRecording();
        }

        activeOnComplete = onComplete;
        activeRecordCoroutine = StartCoroutine(RecordRoutine(onComplete));
    }

    // Cancels an in-progress recording without invoking onComplete.
    // Use this when the flow moves on and the result would no longer be wanted
    // (e.g. ConversationScreen disabling because the session moved past
    // a state that expects audio).
    public void StopRecording()
    {
        if (!isRecording && activeRecordCoroutine == null)
            return; // nothing to stop — safe no-op

        if (activeRecordCoroutine != null)
        {
            StopCoroutine(activeRecordCoroutine);
            activeRecordCoroutine = null;
        }

        if (Microphone.IsRecording(micDevice))
            Microphone.End(micDevice);

        isRecording = false;
        activeOnComplete = null; // deliberately do NOT invoke — this is a cancellation, not a completion/failure
    }

    private IEnumerator RecordRoutine(Action<byte[]> onComplete)
    {
        isRecording = true;

        // IMPORTANT: loop = true.
        // With loop = false, Unity auto-stops the clip once maxRecordingLength
        // is reached, which can race against our own timer-based stop and leave
        // Microphone.GetPosition() returning 0 (=> "No audio recorded" even
        // though the mic was live the whole time). Looping means only our own
        // Microphone.End() call below stops it.
        recordingClip = Microphone.Start(
            micDevice,
            true,
            Mathf.CeilToInt(maxRecordingLength) + 1,
            sampleRate
        );

        // wait for mic to actually start
        while (Microphone.GetPosition(micDevice) <= 0) yield return null;

        float elapsed = 0f;
        float silenceTimer = 0f;
        bool speechDetected = false;

        const float pollInterval = 0.1f; // 100ms averaging window, matches Python reference script
        int windowSize = Mathf.RoundToInt(sampleRate * pollInterval);
        float[] sampleWindow = new float[windowSize];

        const int safetyMargin = 256;      // stay this many samples behind the write head
        float pollTimer = 0f;

        while (elapsed < maxRecordingLength)
        {
            pollTimer += Time.deltaTime;

            if (pollTimer >= pollInterval)
            {
                pollTimer = 0f;

                int micPos = Microphone.GetPosition(micDevice) - sampleWindow.Length - safetyMargin;
                int clipSamples = recordingClip.samples;

                if (micPos >= 0 && micPos + sampleWindow.Length <= clipSamples)
                {
                    recordingClip.GetData(sampleWindow, micPos);
                    float volume = 0f;
                    foreach (float s in sampleWindow) volume += Mathf.Abs(s);
                    volume /= sampleWindow.Length;

                    if (logVolumeForCalibration)
                    {
                        Debug.Log(
                            $"[AudioRecorder] volume={volume:F4} " +
                            $"threshold={silenceThreshold:F4} " +
                            $"speechDetected={speechDetected} " +
                            $"silenceTimer={silenceTimer:F2}"
                        );
                    }

                    if (volume > silenceThreshold)
                    {
                        speechDetected = true;
                        silenceTimer = 0f;
                    }
                    else if (speechDetected)
                    {
                        silenceTimer += pollInterval;
                        if (silenceTimer >= silenceDurationToStop && elapsed >= minRecordingLength)
                            break;
                    }
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        int finalPos = Microphone.GetPosition(micDevice);
        Microphone.End(micDevice);
        isRecording = false;
        activeRecordCoroutine = null;

        // No usable audio, or nobody actually spoke above threshold.
        // Sending near-silent clips to the backend risks ASR hallucinations
        // (e.g. Whisper returning "music" on silence), so we discard here
        // instead of sending.
        if (finalPos <= 0 || !speechDetected)
        {
            if (!speechDetected && finalPos > 0)
            {
                Debug.Log(
                    "[AudioRecorder] No speech detected above threshold — " +
                    "discarding clip instead of sending to backend."
                );
            }

            onComplete?.Invoke(null);
            yield break;
        }

        float[] finalSamples = new float[finalPos];
        recordingClip.GetData(finalSamples, 0);

        byte[] wavBytes = EncodeToWav(finalSamples, sampleRate, 1);
        onComplete?.Invoke(wavBytes);
    }

    private byte[] EncodeToWav(float[] samples, int sampleRate, int channels)
    {
        using (MemoryStream stream = new MemoryStream())
        {
            int byteRate = sampleRate * channels * 2;
            int dataSize = samples.Length * 2;

            stream.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"), 0, 4);
            stream.Write(BitConverter.GetBytes(36 + dataSize), 0, 4);
            stream.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"), 0, 4);
            stream.Write(System.Text.Encoding.ASCII.GetBytes("fmt "), 0, 4);
            stream.Write(BitConverter.GetBytes(16), 0, 4);
            stream.Write(BitConverter.GetBytes((short)1), 0, 2);
            stream.Write(BitConverter.GetBytes((short)channels), 0, 2);
            stream.Write(BitConverter.GetBytes(sampleRate), 0, 4);
            stream.Write(BitConverter.GetBytes(byteRate), 0, 4);
            stream.Write(BitConverter.GetBytes((short)(channels * 2)), 0, 2);
            stream.Write(BitConverter.GetBytes((short)16), 0, 2);
            stream.Write(System.Text.Encoding.ASCII.GetBytes("data"), 0, 4);
            stream.Write(BitConverter.GetBytes(dataSize), 0, 4);

            foreach (float sample in samples)
            {
                short val = (short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue);
                stream.Write(BitConverter.GetBytes(val), 0, 2);
            }

            return stream.ToArray();
        }
    }
}