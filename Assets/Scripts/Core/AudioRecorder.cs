using UnityEngine;
using System;
using System.Collections;
using System.IO;

public class AudioRecorder : MonoBehaviour
{
    public static AudioRecorder Instance;

    [Header("Recording Settings")]
    public int sampleRate = 16000; // Whisper prefers 16kHz
    public float silenceThreshold = 0.03f;
    public float silenceDurationToStop = 1.8f;
    public float maxRecordingLength = 15f;
    public float minRecordingLength = 0.5f; // avoid sending empty/near-empty clips

    private string micDevice;
    private AudioClip recordingClip;
    private bool isRecording = false;

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

        StartCoroutine(RecordRoutine(onComplete));
    }

    private IEnumerator RecordRoutine(Action<byte[]> onComplete)
    {
        isRecording = true;
        recordingClip = Microphone.Start(micDevice, false, Mathf.CeilToInt(maxRecordingLength), sampleRate);

        // wait for mic to actually start
        while (Microphone.GetPosition(micDevice) <= 0) yield return null;

        float elapsed = 0f;
        float silenceTimer = 0f;
        bool speechDetected = false;
        float[] sampleWindow = new float[128];

        while (elapsed < maxRecordingLength)
        {
            int micPos = Microphone.GetPosition(micDevice) - sampleWindow.Length;
            if (micPos >= 0)
            {
                recordingClip.GetData(sampleWindow, micPos);
                float volume = 0f;
                foreach (float s in sampleWindow) volume += Mathf.Abs(s);
                volume /= sampleWindow.Length;
                if (volume > silenceThreshold)
                {
                    speechDetected = true;
                    silenceTimer = 0f;
                }
                else if (speechDetected)
                {
                    silenceTimer += Time.deltaTime;
                    if (silenceTimer >= silenceDurationToStop && elapsed >= minRecordingLength)
                        break;
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        int finalPos = Microphone.GetPosition(micDevice);
        Microphone.End(micDevice);
        isRecording = false;

        if (finalPos <= 0)
        {
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