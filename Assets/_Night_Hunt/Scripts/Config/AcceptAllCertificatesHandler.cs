using UnityEngine.Networking;

namespace NightHunt.Config
{
    /// <summary>
    /// CertificateHandler that bypasses SSL validation.
    /// Dùng khi server cloud dùng self-signed cert (mkcert) với IP trực tiếp,
    /// chưa có domain + Let's Encrypt.
    ///
    /// ⚠️  CHỈ dùng cho dev/staging. KHÔNG dùng cho Production release.
    ///     Kiểm soát bằng BackendConfig.allowSelfSignedCert.
    /// </summary>
    public class AcceptAllCertificatesHandler : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData) => true;
    }
}
