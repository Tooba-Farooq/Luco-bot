using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

[RequireComponent(typeof(RectTransform))]
public class ButtonPressBounce : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public float pressedScale = 0.92f;
    public float animDuration = 0.08f;

    private RectTransform rect;
    private Vector3 originalScale;
    private Coroutine activeRoutine;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        originalScale = rect.localScale;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        AnimateTo(originalScale * pressedScale);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        AnimateTo(originalScale);
    }

    private void AnimateTo(Vector3 target)
    {
        if (activeRoutine != null) StopCoroutine(activeRoutine);
        activeRoutine = StartCoroutine(ScaleRoutine(target));
    }

    private IEnumerator ScaleRoutine(Vector3 target)
    {
        Vector3 start = rect.localScale;
        float t = 0f;

        while (t < animDuration)
        {
            t += Time.deltaTime;
            rect.localScale = Vector3.Lerp(start, target, t / animDuration);
            yield return null;
        }

        rect.localScale = target;
    }
}