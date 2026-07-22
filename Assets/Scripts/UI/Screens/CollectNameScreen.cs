using UnityEngine;
using TMPro;

public class CollectNameScreen : MonoBehaviour
{
    public TMP_InputField nameInputField;
    public VisitorFlowManager flowManager;
    public FaceExpressionController face;

    [Header("Audio")]
    public AudioClip whatsYourNameClip; // ADD

    void OnEnable()
    {
        nameInputField.text = "";
        nameInputField.Select();
        nameInputField.gameObject.SetActive(true);

        if (face != null && whatsYourNameClip != null)
            face.StartTalking(whatsYourNameClip); // ADD
    }

    public void OnSubmit()
    {
        string name = nameInputField.text.Trim();

        if (string.IsNullOrEmpty(name))
        {
            Debug.LogWarning("Name field empty, not submitting.");
            return;
        }

        if (flowManager == null)
        {
            Debug.LogError("CRITICAL: flowManager field is BLANK in Screen_CollectName Inspector!");
            return;
        }

        flowManager.Session.visitorName = name;
        Debug.Log("Visitor name captured: " + name);

        flowManager.GoTo(VisitorFlowState.ConfirmSpelling);
    }

    public void ResetScreen()
    {
        nameInputField.text = "";
    }
}