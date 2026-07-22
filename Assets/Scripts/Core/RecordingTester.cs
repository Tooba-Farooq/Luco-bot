using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.IO;

public class RecordingTester : MonoBehaviour
{
    public Button recordButton;
    public Button playButton;
    public TMP_Text statusText;

    private byte[] recordedWav;
    private AudioSource audioSource;

    void Start()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        recordButton.onClick.AddListener(StartTestRecording);
        playButton.onClick.AddListener(PlayBack);
    }

    void StartTestRecording()
    {
        statusText.text = "Recording... talk now, then go quiet.";
        AudioRecorder.Instance.StartRecording(OnRecordingComplete);
    }

    void OnRecordingComplete(byte[] wavBytes)
    {
        if (wavBytes == null)
        {
            statusText.text = "FAILED - no audio captured (check mic permissions/device).";
            return;
        }

        recordedWav = wavBytes;
        statusText.text = "Done. Bytes: " + wavBytes.Length + " (~" + (wavBytes.Length / 32000f).ToString("F1") + "s)";

        // Save to disk so you can also check it outside Unity
        string path = Application.persistentDataPath + "/test_recording.wav";
        File.WriteAllBytes(path, wavBytes);
        Debug.Log("Saved WAV to: " + path);
    }

    void PlayBack()
    {
        if (recordedWav == null)
        {
            statusText.text = "No recording yet.";
            return;
        }

        AudioClip clip = WavBytesToAudioClip(recordedWav);
        if (clip == null)
        {
            statusText.text = "Could not decode WAV for playback.";
            return;
        }

        audioSource.clip = clip;
        audioSource.Play();
        statusText.text = "Playing...";
    }

    // Minimal WAV (16-bit PCM, mono) decoder matching AudioRecorder's EncodeToWav format
    private AudioClip WavBytesToAudioClip(byte[] wavBytes)
    {
        try
        {
            int channels = BitConverter.ToInt16(wavBytes, 22);
            int sampleRate = BitConverter.ToInt32(wavBytes, 24);
            int dataStart = 44; // fixed header size, matches AudioRecorder's EncodeToWav
            int dataSize = wavBytes.Length - dataStart;
            int sampleCount = dataSize / 2; // 16-bit = 2 bytes per sample

            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                short val = BitConverter.ToInt16(wavBytes, dataStart + i * 2);
                samples[i] = val / (float)short.MaxValue;
            }

            AudioClip clip = AudioClip.Create("Playback", sampleCount, channels, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
        catch (Exception e)
        {
            Debug.LogError("WAV decode failed: " + e.Message);
            return null;
        }
    }
}