using UnityEngine;

public class UIManager : MonoBehaviour
{
    public void ShowScreen(VisitorFlowState state)
    {
        Debug.Log("Would show screen for state: " + state);
        // Real implementation later: activate/deactivate the matching UI panel
    }
}