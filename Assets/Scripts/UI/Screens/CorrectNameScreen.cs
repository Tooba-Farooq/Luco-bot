using UnityEngine;
using TMPro;

public class CorrectNameScreen : MonoBehaviour
{
    public TMP_InputField nameInputField;
    public VisitorFlowManager flowManager;

    void OnEnable()
    {
        nameInputField.text = flowManager.Session.visitorName; // pre-fill with what was captured
        nameInputField.Select();
    }

    public void OnSubmit()
    {
        string name = nameInputField.text.Trim();

        if (string.IsNullOrEmpty(name))
        {
            Debug.LogWarning("Name field empty, not submitting.");
            return;
        }

        flowManager.Session.visitorName = name;
        Debug.Log("Corrected name: " + name);

        flowManager.GoTo(VisitorFlowState.ConfirmSpelling); // loop back to confirm the fixed name
    }
}