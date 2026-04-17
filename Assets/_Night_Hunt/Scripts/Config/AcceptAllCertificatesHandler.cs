using UnityEngine.Networking;

namespace NightHunt.Config
{
    /// <summary>
    /// CertificateHandler that bypasses SSL validation.
    /// Uses khi server cloud dùng self-signed cert (mkcert) với IP trực tiếp,
    /// not yet available domain + Let's Encrypt.
    ///
    /// ⚠️  CHỈ dùng cho dev/staging. KHÔNG dùng cho Production release.
    ///     Kiểm soát bằng BackendConfig.allowSelfSignedCert.
    /// </summary>
    public class AcceptAllCertificatesHandler : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData) => true;
    }
}
