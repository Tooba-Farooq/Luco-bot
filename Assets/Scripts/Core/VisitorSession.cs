using UnityEngine;

[System.Serializable]
public class VisitorSession
{
    public bool isKnownVisitor;
    public string visitorName;
    public Texture2D visitorPhoto;
    public string purpose;
    public string hostName;
    public int alertRetryCount = 0;
    public bool hostAvailable;
    public string message;
    public string qrBase64;

    public void Reset()
    {
        isKnownVisitor = false;
        visitorName = null;
        visitorPhoto = null;
        purpose = null;
        hostName = null;
        alertRetryCount = 0;
        hostAvailable = false;
        message = null;
        qrBase64 = null;
    }
}