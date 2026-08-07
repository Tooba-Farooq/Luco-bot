using UnityEngine;
using UnityEngine.UI;
using uLipSync;

public class LipSyncMouthReceiver : MonoBehaviour
{
    [Header("Target")]
    public Image mouthTalkImage;

    [Header("Viseme Sprites")]
    public Sprite mouthA;
    public Sprite mouthI;
    public Sprite mouthU;
    public Sprite mouthE;
    public Sprite mouthO;
    public Sprite mouthClosed;

    // Wire this to uLipSync's "On Lip Sync Update" event in the Inspector.
    public void OnLipSyncUpdate(LipSyncInfo info)
    {
        if (mouthTalkImage == null) return;
        if (info.volume < Mathf.Epsilon)
        {
            mouthTalkImage.sprite = mouthClosed;
            return;
        }

        switch (info.phoneme)
        {
            case "A": mouthTalkImage.sprite = mouthA; break;
            case "I": mouthTalkImage.sprite = mouthI; break;
            case "U": mouthTalkImage.sprite = mouthU; break;
            case "E": mouthTalkImage.sprite = mouthE; break;
            case "O": mouthTalkImage.sprite = mouthO; break;
            default:  mouthTalkImage.sprite = mouthClosed; break;
        }
    }
}