using UnityEngine;

public class AndroidTTS : MonoBehaviour
{
    public static AndroidTTS Instance;

    private AndroidJavaObject ttsObject;
    private bool isReady = false;

    void Awake()
    {
        Instance = this;

#if UNITY_ANDROID && !UNITY_EDITOR
        InitTTS();
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private void InitTTS()
    {
        AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

        ttsObject = new AndroidJavaObject("android.speech.tts.TextToSpeech", currentActivity, null);
        isReady = true; // simplified — good enough for a demo; production would wait for OnInit callback
    }
#endif

    public void Speak(string text)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (ttsObject != null)
        {
            AndroidJavaObject bundle = new AndroidJavaObject("android.os.Bundle");
            ttsObject.Call<int>("speak", text, 0, bundle, "utteranceId");
        }
#else
        Debug.Log("[TTS - Editor stub] Would speak: " + text);
#endif
    }
}