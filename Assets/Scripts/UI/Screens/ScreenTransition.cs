using UnityEngine;
using System;

[RequireComponent(typeof(Animator))]
public class ScreenTransition : MonoBehaviour
{
    private Animator animator;
    private static readonly int ShowTrigger = Animator.StringToHash("Show");
    private static readonly int HideTrigger = Animator.StringToHash("Hide");

    public event Action OnHideComplete;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    public void PlayShow()
    {
        gameObject.SetActive(true);
        animator.ResetTrigger(HideTrigger);
        animator.SetTrigger(ShowTrigger);
    }

    public void PlayHide()
    {
        // Object stays active until the Hide clip's Animation Event fires below —
        // this is what lets coroutines on it (QRCodeScreen's ReturnToIdleAfterDelay, etc.)
        // finish cleanly instead of being killed mid-flight by an instant SetActive(false).
        animator.ResetTrigger(ShowTrigger);
        animator.SetTrigger(HideTrigger);
    }

    // Wire this as an Animation Event at the LAST FRAME of the Hide clip
    public void OnHideAnimationComplete()
    {
        gameObject.SetActive(false);
        OnHideComplete?.Invoke();
    }
}