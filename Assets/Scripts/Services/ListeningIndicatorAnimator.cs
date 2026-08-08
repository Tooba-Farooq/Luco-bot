using UnityEngine;
using UnityEngine.UI;

public class ListeningIndicatorAnimator : MonoBehaviour
{
    [Header("Mic Icon (optional pulse)")]
    public RectTransform micIcon;
    public float micPulseScale = 1.15f;
    public float micPulseSpeed = 2f;
    [Header("Waveform Bars")]
    public RectTransform[] waveformBars;
    public float minBarHeight = 4f;
    public float maxBarHeight = 20f;
    public float animationSpeed = 1.5f;
    private float[] barPhaseOffsets;
    private float[] barSpeedMultipliers;

    void OnEnable()
    {
        barPhaseOffsets = new float[waveformBars.Length];
        barSpeedMultipliers = new float[waveformBars.Length];

        for (int i = 0; i < waveformBars.Length; i++)
        {
            barPhaseOffsets[i] = Random.Range(0f, Mathf.PI * 2f);
            barSpeedMultipliers[i] = Random.Range(0.8f, 1.4f);
        }
    }

    void Update()
    {
        AnimateWaveform();
    }

    void AnimateWaveform()
{
    if (waveformBars == null || AudioRecorder.Instance == null) return;

    float[] volumeHistory = AudioRecorder.Instance.GetVolumeHistory();
    int sampleCount = Mathf.Min(volumeHistory.Length, waveformBars.Length);

    for (int i = 0; i < waveformBars.Length; i++)
    {
        if (waveformBars[i] == null) continue;

        float normalized;

        if (i < sampleCount)
        {
            // scale relative to silence threshold so it reacts sensibly
            // regardless of the raw volume range
            normalized = Mathf.Clamp01(
                volumeHistory[i] / (AudioRecorder.Instance.silenceThreshold * 6f)
            );
        }
        else
        {
            normalized = 0f;
        }

        float targetHeight = Mathf.Lerp(minBarHeight, maxBarHeight, normalized);

        Vector2 size = waveformBars[i].sizeDelta;
        size.y = Mathf.Lerp(size.y, targetHeight, Time.deltaTime * animationSpeed);
        waveformBars[i].sizeDelta = size;
    }
}

    void AnimateMicPulse()
    {
        if (micIcon == null) return;

        float pulse = 1f + (Mathf.Sin(Time.time * micPulseSpeed) * 0.5f + 0.5f) * (micPulseScale - 1f);
        micIcon.localScale = new Vector3(pulse, pulse, 1f);
    }
}