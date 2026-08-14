using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class AudioRecorder : MonoBehaviour
{
    public static AudioRecorder Instance;

    [Header("Recording Settings")]
    public int sampleRate = 16000; // Whisper prefers 16kHz
    public float silenceDurationToStop = 1.4f;
    public float maxRecordingLength = 20f;
    public float minRecordingLength = 0.5f;

    [Header("Adaptive Threshold")]
    [Tooltip("How far above the tracked noise floor a window must be to count as speech.")]
    public float noiseFloorMultiplier = 3.0f;
    [Tooltip("Absolute minimum threshold, protects against near-zero noise floors in dead-silent rooms.")]
    public float minAbsoluteThreshold = 0.004f;
    [Tooltip("Absolute maximum threshold, protects against a runaway noise floor (e.g. mic glitch).")]
    public float maxAbsoluteThreshold = 0.05f;
    [Tooltip("How many consecutive loud windows are needed before we commit to 'speech started'. Filters out single spikes.")]
    public int speechStartDebounceWindows = 2;

    [Header("Initial Calibration")]
    public float calibrationDuration = 0.6f;

    [Header("Continuous Recalibration")]
    [Tooltip("While in 'quiet' state, update the noise floor using a rolling median of recent quiet windows.")]
    public bool continuousRecalibration = true;
    [Tooltip("Number of recent quiet windows kept for the rolling noise floor estimate.")]
    public int rollingNoiseFloorWindowCount = 20;

    [Header("Device Selection")]
    [Tooltip("Leave blank to use system default microphone.")]
    public string preferredMicDevice = "";

    [Header("Debug")]
    public bool logVolumeForCalibration = false;

    [Header("Waveform Data")]
    public int volumeHistorySize = 12;

    public float CurrentVolume { get; private set; }
    public float CurrentThreshold { get; private set; }

    private float[] volumeHistoryBuffer;
    private int volumeHistoryIndex;
    private string micDevice;
    private AudioClip recordingClip;
    private bool isRecording = false;
    private Coroutine activeRecordCoroutine;
    private Action<byte[]> activeOnComplete;

    private readonly List<float> rollingQuietSamples = new List<float>();

    // Zero-alloc cached buffers
    private float[] medianSortBuffer;
    private WaitForSeconds cachedPollWait;
    
    // Cached WAV header ASCII bytes
    private static readonly byte[] RIFF = new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' };
    private static readonly byte[] WAVE = new byte[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' };
    private static readonly byte[] FMT  = new byte[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' };
    private static readonly byte[] DATA = new byte[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' };

    private const float POLL_INTERVAL = 0.1f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        cachedPollWait = new WaitForSeconds(POLL_INTERVAL);
        SelectMicrophoneDevice();
    }

    private void OnDisable() => StopRecording();
    private void OnDestroy() => StopRecording();

    public void SelectMicrophoneDevice()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("[AudioRecorder] No microphone input devices found on system!");
            micDevice = null;
            return;
        }

        micDevice = Microphone.devices[0];
        if (!string.IsNullOrEmpty(preferredMicDevice))
        {
            foreach (string device in Microphone.devices)
            {
                if (device.Equals(preferredMicDevice, StringComparison.OrdinalIgnoreCase))
                {
                    micDevice = device;
                    break;
                }
            }
        }

        Debug.Log($"[AudioRecorder] Active Microphone Device: '{micDevice}'");
    }

    public void StartRecording(Action<byte[]> onComplete)
    {
        if (string.IsNullOrEmpty(micDevice))
        {
            SelectMicrophoneDevice();
            if (string.IsNullOrEmpty(micDevice))
            {
                Debug.LogWarning("[AudioRecorder] Cannot start recording: No microphone device available.");
                onComplete?.Invoke(null);
                return;
            }
        }

        if (isRecording)
        {
            Debug.LogWarning("[AudioRecorder] StartRecording called while active. Resetting session.");
            ResetRecordingState(false);
        }

        activeOnComplete = onComplete;
        activeRecordCoroutine = StartCoroutine(RecordRoutine(onComplete));
    }

    public void StopRecording()
    {
        ResetRecordingState(true);
    }

    private void ResetRecordingState(bool invokeCallback)
    {
        if (activeRecordCoroutine != null)
        {
            StopCoroutine(activeRecordCoroutine);
            activeRecordCoroutine = null;
        }

        if (!string.IsNullOrEmpty(micDevice) && Microphone.IsRecording(micDevice))
        {
            Microphone.End(micDevice);
        }

        if (recordingClip != null)
        {
            Destroy(recordingClip);
            recordingClip = null;
        }

        isRecording = false;

        if (invokeCallback)
        {
            activeOnComplete?.Invoke(null);
        }
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

    // ---- Helpers ----

    private float CalculateMedianZeroAlloc(List<float> values)
    {
        if (values == null || values.Count == 0) return 0f;
        int count = values.Count;

        if (medianSortBuffer == null || medianSortBuffer.Length < count)
        {
            medianSortBuffer = new float[count];
        }

        for (int i = 0; i < count; i++) medianSortBuffer[i] = values[i];
        Array.Sort(medianSortBuffer, 0, count);

        int mid = count / 2;
        return (count % 2 == 0) ? (medianSortBuffer[mid - 1] + medianSortBuffer[mid]) / 2f : medianSortBuffer[mid];
    }

    private float ComputeThresholdFromNoiseFloor(float noiseFloor)
    {
        float raw = noiseFloor * noiseFloorMultiplier;
        return Mathf.Clamp(raw, minAbsoluteThreshold, maxAbsoluteThreshold);
    }

    private void PushQuietSample(float volume)
    {
        rollingQuietSamples.Add(volume);
        if (rollingQuietSamples.Count > rollingNoiseFloorWindowCount)
            rollingQuietSamples.RemoveAt(0);
    }

    private bool TryGetSampleWindow(float[] outputBuffer, int safetyMargin = 256)
{
    if (recordingClip == null || string.IsNullOrEmpty(micDevice)) return false;

    int headPos = Microphone.GetPosition(micDevice);
    int windowSize = outputBuffer.Length;

    if (headPos < 0) return false;

    int readStart = headPos - windowSize - safetyMargin;

    // Recording sessions are always shorter than the mic's ring buffer
    // (maxRecordingLength + 5s), so the buffer never actually wraps mid-session.
    // A negative readStart just means we don't have enough audio yet
    // (the very start of recording) — skip this window instead of reading
    // stale/unwritten data from the tail of the buffer.
    if (readStart < 0) return false;

    if (readStart + windowSize > recordingClip.samples) return false;

    recordingClip.GetData(outputBuffer, readStart);
    return true;
}

    // ---- Main Routine ----

    private IEnumerator RecordRoutine(Action<byte[]> onComplete)
    {
        isRecording = true;

        volumeHistoryBuffer = new float[volumeHistorySize];
        volumeHistoryIndex = 0;
        CurrentVolume = 0f;
        rollingQuietSamples.Clear();

        int maxBufferLengthSec = Mathf.CeilToInt(maxRecordingLength) + 5;
        
        // Loop set to TRUE to prevent headPos locking at buffer end
        recordingClip = Microphone.Start(micDevice, true, maxBufferLengthSec, sampleRate);

        while (Microphone.GetPosition(micDevice) <= 0)
            yield return null;

        int windowSize = Mathf.RoundToInt(sampleRate * POLL_INTERVAL);
        float[] sampleWindow = new float[windowSize];

        // --- Initial Calibration ---
        int calibrationSteps = Mathf.Max(1, Mathf.RoundToInt(calibrationDuration / POLL_INTERVAL));
        var calibSamples = new List<float>(calibrationSteps);

        for (int i = 0; i < calibrationSteps; i++)
        {
            yield return cachedPollWait;
            if (TryGetSampleWindow(sampleWindow))
            {
                float v = 0f;
                for (int s = 0; s < sampleWindow.Length; s++) v += Mathf.Abs(sampleWindow[s]);
                v /= sampleWindow.Length;
                calibSamples.Add(v);
            }
        }

        if (calibSamples.Count == 0) calibSamples.Add(minAbsoluteThreshold);

        float initialNoiseFloor = CalculateMedianZeroAlloc(calibSamples);
        rollingQuietSamples.AddRange(calibSamples);
        float effectiveThreshold = ComputeThresholdFromNoiseFloor(initialNoiseFloor);
        CurrentThreshold = effectiveThreshold;

        if (logVolumeForCalibration)
        {
            Debug.Log($"[AudioRecorder] Initial noise floor (median)={initialNoiseFloor:F5} Threshold={effectiveThreshold:F5}");
        }

        // --- Recording Loop ---
        float elapsed = calibrationDuration;
        float silenceTimer = 0f;
        bool speechDetected = false;
        int consecutiveLoudWindows = 0;

        while (elapsed < maxRecordingLength)
        {
            yield return cachedPollWait;
            elapsed += POLL_INTERVAL;

            if (TryGetSampleWindow(sampleWindow))
            {
                float volume = 0f;
                for (int i = 0; i < sampleWindow.Length; i++) volume += Mathf.Abs(sampleWindow[i]);
                volume /= sampleWindow.Length;

                CurrentVolume = volume;
                volumeHistoryBuffer[volumeHistoryIndex] = volume;
                volumeHistoryIndex = (volumeHistoryIndex + 1) % volumeHistoryBuffer.Length;

                bool isLoud = volume > effectiveThreshold;

                if (isLoud)
                {
                    consecutiveLoudWindows++;
                    silenceTimer = 0f;

                    if (consecutiveLoudWindows >= speechStartDebounceWindows)
                        speechDetected = true;
                }
                else
                {
                    consecutiveLoudWindows = 0;

                    if (continuousRecalibration && !speechDetected)
                    {
                        PushQuietSample(volume);
                        float rollingFloor = CalculateMedianZeroAlloc(rollingQuietSamples);
                        effectiveThreshold = ComputeThresholdFromNoiseFloor(rollingFloor);
                        CurrentThreshold = effectiveThreshold;
                    }

                    if (speechDetected)
                    {
                        silenceTimer += POLL_INTERVAL;
                        if (silenceTimer >= silenceDurationToStop && elapsed >= minRecordingLength)
                            break;
                    }
                }

                if (logVolumeForCalibration)
                {
                    Debug.Log($"[AudioRecorder] vol={volume:F5} thresh={effectiveThreshold:F5} speech={speechDetected} silence={silenceTimer:F2}");
                }
            }
        }

        int finalPos = Microphone.GetPosition(micDevice);
        Microphone.End(micDevice);
        isRecording = false;
        activeRecordCoroutine = null;

        int totalMaxSamples = Mathf.CeilToInt(maxRecordingLength) * sampleRate;
        int recordedSampleCount = Mathf.Min(finalPos > 0 ? finalPos : totalMaxSamples, totalMaxSamples);

        if (recordedSampleCount <= 0 || !speechDetected)
        {
            if (!speechDetected && recordedSampleCount > 0)
            {
                Debug.Log("[AudioRecorder] No speech detected above threshold — discarding clip.");
            }

            if (recordingClip != null)
            {
                Destroy(recordingClip);
                recordingClip = null;
            }

            activeOnComplete = null;
            onComplete?.Invoke(null);
            yield break;
        }

        float[] finalSamples = new float[recordedSampleCount];
        recordingClip.GetData(finalSamples, 0);

        Destroy(recordingClip);
        recordingClip = null;

        NormalizeGain(finalSamples);

        byte[] wavBytes = EncodeToWavZeroAlloc(finalSamples, sampleRate, 1);
        
        activeOnComplete = null;
        onComplete?.Invoke(wavBytes);
    }

    private void NormalizeGain(float[] samples, float targetPeak = 0.95f)
    {
        float peak = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float abs = Mathf.Abs(samples[i]);
            if (abs > peak) peak = abs;
        }

        if (peak < 0.0001f) return;

        float scale = targetPeak / peak;

        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = Mathf.Clamp(samples[i] * scale, -1f, 1f);
        }
    }

    private byte[] EncodeToWavZeroAlloc(float[] samples, int sampleRate, int channels)
    {
        int headerSize = 44;
        int dataSize = samples.Length * 2;
        byte[] wav = new byte[headerSize + dataSize];

        // RIFF header
        Buffer.BlockCopy(RIFF, 0, wav, 0, 4);
        WriteInt(wav, 4, 36 + dataSize);
        Buffer.BlockCopy(WAVE, 0, wav, 8, 4);
        Buffer.BlockCopy(FMT, 0, wav, 12, 4);
        WriteInt(wav, 16, 16); 
        WriteShort(wav, 20, 1); 
        WriteShort(wav, 22, (short)channels);
        WriteInt(wav, 24, sampleRate);
        WriteInt(wav, 28, sampleRate * channels * 2); 
        WriteShort(wav, 32, (short)(channels * 2)); 
        WriteShort(wav, 34, 16); 
        Buffer.BlockCopy(DATA, 0, wav, 36, 4);
        WriteInt(wav, 40, dataSize);

        // PCM Data (Corrected scale multiplier to 32767f to avoid PCM integer overflow)
        int offset = 44;
        for (int i = 0; i < samples.Length; i++)
        {
            int pcmVal = Mathf.Clamp((int)(samples[i] * 32767f), -32768, 32767);
            wav[offset]     = (byte)(pcmVal & 0xFF);
            wav[offset + 1] = (byte)((pcmVal >> 8) & 0xFF);
            offset += 2;
        }

        return wav;
    }

    private void WriteInt(byte[] buffer, int offset, int value)
    {
        buffer[offset]     = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
        buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
    }

    private void WriteShort(byte[] buffer, int offset, short value)
    {
        buffer[offset]     = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
    }
}