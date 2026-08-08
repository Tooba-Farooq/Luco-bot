using UnityEngine;
using TMPro;
using System.Collections;

public class CaptionBarController : MonoBehaviour
{
    [Header("References")]
    public GameObject captionBar;
    public TMP_Text captionText;
    public GameObject listeningIndicator;

    [Header("Typing Animation")]
    public bool useTypingAnimation = true;
    public float minCharsPerSecond = 30f;

    private Coroutine typingRoutine;

    public void ShowCaption(string text, float syncDuration = -1f)
    {
        if (captionBar == null || captionText == null) return;

        HideListening();

        if (typingRoutine != null)
            StopCoroutine(typingRoutine);

        captionBar.SetActive(true);
        captionText.gameObject.SetActive(true);

        if (string.IsNullOrEmpty(text))
        {
            captionText.text = "";
            return;
        }

        if (useTypingAnimation)
        {
            typingRoutine = StartCoroutine(TypeText(text, syncDuration));
        }
        else
        {
            captionText.text = text;
        }
    }

    public void HideCaption()
    {
        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        if (captionBar != null && listeningIndicator != null && !listeningIndicator.activeSelf)
            captionBar.SetActive(false);

        if (captionText != null)
            captionText.gameObject.SetActive(false);
    }

    public void ShowListening()
    {
        if (captionBar == null || listeningIndicator == null) return;

        HideCaption();

        captionBar.SetActive(true);
        listeningIndicator.SetActive(true);
    }

    public void HideListening()
    {
        if (listeningIndicator != null)
            listeningIndicator.SetActive(false);

        if (captionBar != null && captionText != null && !captionText.gameObject.activeSelf)
            captionBar.SetActive(false);
    }

    private IEnumerator TypeText(string text, float syncDuration)
    {
        captionText.text = "";
        int totalChars = text.Length;

        float charsPerSecond = syncDuration > 0f
            ? Mathf.Max(totalChars / syncDuration, minCharsPerSecond)
            : minCharsPerSecond;

        float elapsed = 0f;
        int shown = 0;

        while (shown < totalChars)
        {
            elapsed += Time.deltaTime;
            int targetShown = Mathf.Min(totalChars, Mathf.FloorToInt(elapsed * charsPerSecond));
            if (targetShown > shown)
            {
                shown = targetShown;
                captionText.text = text.Substring(0, shown);
            }
            yield return null;
        }

        typingRoutine = null;
    }
}