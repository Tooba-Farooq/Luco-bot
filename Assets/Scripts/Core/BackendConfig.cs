using UnityEngine;
using UnityEngine.UI;
using TMPro; 

public class BackendConfig : MonoBehaviour
{
    public TMP_InputField ipInputField; 
    public FaceDetectionService detectionService;

    public GameObject configPanel;

    private const string IP_KEY = "backend_ip";

    void Start()
    {
        string savedIp = PlayerPrefs.GetString(IP_KEY, "");
        ipInputField.text = savedIp;
        ApplyIp(savedIp);
    }

    public void OnIpConfirmed()
    {
        string ip = ipInputField.text.Trim();

        if (string.IsNullOrEmpty(ip))
        {
            Debug.LogWarning("IP field is empty, not applying.");
            return;
        }

        PlayerPrefs.SetString(IP_KEY, ip);
        ApplyIp(ip);
        TogglePanel();
    }

    public void TogglePanel()
    {
        configPanel.SetActive(!configPanel.activeSelf);
    }

    private void ApplyIp(string ip)
    {
        detectionService.baseUrl = $"http://{ip}:8000";
        Debug.Log("Backend URL set to: " + detectionService.baseUrl);
    }
}