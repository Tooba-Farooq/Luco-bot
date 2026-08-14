using UnityEngine.Networking;

public class BypassCertificate : CertificateHandler
{
    protected override bool ValidateCertificate(byte[] certificateData)
    {
        return true; // dev-only — skips cert validation
    }
}