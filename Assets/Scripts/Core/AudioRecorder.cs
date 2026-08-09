using UnityEngine;
using System;
using System.Collections;
using System.IO;

public class AudioRecorder : MonoBehaviour
{
    public static AudioRecorder Instance;

    [Header("Recording Settings")]
    public int sampleRate = 16000; // Whisper prefers 16kHz
    public float silenceThreshold = 0.02f; // Fallback minimum
    public float silenceDurationToStop = 1.8f;
    public float maxRecordingLength = 30f;
    public float minRecordingLength = 0.5f;

    [Header("Noise Calibration")]
    public float calibrationDuration = 0.3f;
    public float noiseFloorMultiplier = 3f;

    [Header("Debug")]
    [Tooltip("Logs live volume readings to the console for threshold calibration.")]
    public bool logVolumeForCalibration = false;

    [Header("Waveform Data")]
    public int volumeHistorySize = 12;

    public float CurrentVolume { get; private set; }

    private float[] volumeHistoryBuffer;
    private int volumeHistoryIndex;
    private string micDevice;
    private AudioClip recordingClip;
    private bool isRecording = false;
    private Coroutine activeRecordCoroutine;
    private Action<byte[]> activeOnComplete;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (Microphone.devices.Length > 0)
            micDevice = Microphone.devices[0];
    }

    private void OnDisable()
    {
        StopRecording();
    }

    private void OnDestroy()
    {
        StopRecording();
    }

    public void StartRecording(Action<byte[]> onComplete)
    {
        if (string.IsNullOrEmpty(micDevice))
        {
            Debug.LogWarning("[AudioRecorder] No microphone device available.");
            onComplete?.Invoke(null);
            return;
        }

        if (isRecording)
        {
            Debug.LogWarning("[AudioRecorder] StartRecording called while active. Stopping previous session.");
            StopRecording();
        }

        activeOnComplete = onComplete;
        activeRecordCoroutine = StartCoroutine(RecordRoutine(onComplete));
    }

    public void StopRecording()
    {
        if (!isRecording && activeRecordCoroutine == null)
            return;

        if (activeRecordCoroutine != null)
        {
            StopCoroutine(activeRecordCoroutine);
            activeRecordCoroutine = null;
        }

        if (!string.IsNullOrEmpty(micDevice) && Microphone.IsRecording(micDevice))
        {
            Microphone.End(micDevice);
        }

        isRecording = false;
        activeOnComplete = null;
    }

    public float[] GetVolumeHistory()
    {
        if (volumeHistoryBuffer == null) return new float[volumeHistorySize];

        float[] ordered = new float[volumeHistoryBuffer.Length];
        for (int i = 0; i < volumeHistoryBuffer.Length; i++)
        {
            int idx = (volumeHistoryIndex + i) % volumeHistoryBuffer.Length;
            ordered[i] = volumeHistoryBuffer[idx];
        }
        return ordered;
    }

    private IEnumerator RecordRoutine(Action<byte[]> onComplete)
    {
        isRecording = true;

        volumeHistoryBuffer = new float[volumeHistorySize];
        volumeHistoryIndex = 0;
        CurrentVolume = 0f;

        int maxSamples = Mathf.CeilToInt(maxRecordingLength) * sampleRate;
        
        // Add a safety margin to clip length so loop wrapping never triggers before maxRecordingLength
        recordingClip = Microphone.Start(micDevice, true, Mathf.CeilToInt(maxRecordingLength) + 5, sampleRate);

        while (Microphone.GetPosition(micDevice) <= 0) 
            yield return null;

        const float pollInterval = 0.1f;
        int windowSize = Mathf.RoundToInt(sampleRate * pollInterval);
        float[] sampleWindow = new float[windowSize];
        const int safetyMargin = 256;

        // --- Noise Calibration ---
        float calibElapsed = 0f;
        float noiseFloorSum = 0f;
        int noiseFloorSamples = 0;

        while (calibElapsed < calibrationDuration)
        {
            int micPos = Microphone.GetPosition(micDevice) - windowSize - safetyMargin;
            if (micPos >= 0 && micPos + windowSize <= recordingClip.samples)
            {
                recordingClip.GetData(sampleWindow, micPos);
                float v = 0f;
                foreach (float s in sampleWindow) v += Mathf.Abs(s);
                v /= sampleWindow.Length;

                noiseFloorSum += v;
                noiseFloorSamples++;
            }

            calibElapsed += Time.deltaTime;
            yield return null;
        }

        float noiseFloor = noiseFloorSamples > 0 ? noiseFloorSum / noiseFloorSamples : 0f;
        float effectiveThreshold = Mathf.Max(silenceThreshold, noiseFloor * noiseFloorMultiplier);

        if (logVolumeForCalibration)
        {
            Debug.Log($"[AudioRecorder] Calibrated noiseFloor={noiseFloor:F4} effectiveThreshold={effectiveThreshold:F4}");
        }

        // --- Recording Loop ---
        float elapsed = 0f;
        float silenceTimer = 0f;
        bool speechDetected = false;
        float pollTimer = 0f;

        while (elapsed < maxRecordingLength)
        {
            pollTimer += Time.deltaTime;

            if (pollTimer >= pollInterval)
            {
                pollTimer = 0f;

                int micPos = Microphone.GetPosition(micDevice) - sampleWindow.Length - safetyMargin;

                if (micPos >= 0 && micPos + sampleWindow.Length <= recordingClip.samples)
                {
                    recordingClip.GetData(sampleWindow, micPos);
                    float volume = 0f;
                    foreach (float s in sampleWindow) volume += Mathf.Abs(s);
                    volume /= sampleWindow.Length;

                    CurrentVolume = volume;
                    volumeHistoryBuffer[volumeHistoryIndex] = volume;
                    volumeHistoryIndex = (volumeHistoryIndex + 1) % volumeHistoryBuffer.Length;

                    if (logVolumeForCalibration)
                    {
                        Debug.Log($"[AudioRecorder] vol={volume:F4} thresh={effectiveThreshold:F4} speech={speechDetected} silence={silenceTimer:F2}");
                    }

                    if (volume > effectiveThreshold)
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

        // Clamp final sample length to max allowable samples to prevent overflow read
        int recordedSampleCount = Mathf.Min(finalPos, maxSamples);

        if (recordedSampleCount <= 0 || !speechDetected)
        {
            if (!speechDetected && recordedSampleCount > 0)
            {
                Debug.Log("[AudioRecorder] No speech detected above threshold — discarding clip.");
            }

            onComplete?.Invoke(null);
            yield break;
        }

        float[] finalSamples = new float[recordedSampleCount];
        recordingClip.GetData(finalSamples, 0);

        NormalizeGain(finalSamples);

        byte[] wavBytes = EncodeToWav(finalSamples, sampleRate, 1);
        onComplete?.Invoke(wavBytes);
    }

    private void NormalizeGain(float[] samples, float targetPeak = 0.95f)
    {
        float peak = 0f;
        foreach (float s in samples)
        {
            float abs = Mathf.Abs(s);
            if (abs > peak) peak = abs;
        }

        if (peak < 0.0001f) return;

        float scale = targetPeak / peak;

        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = Mathf.Clamp(samples[i] * scale, -1f, 1f);
        }
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