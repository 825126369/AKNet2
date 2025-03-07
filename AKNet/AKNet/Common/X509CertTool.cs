using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace AKNet.Common
{
    public static class X509CertTool
    {
        public static X509Certificate2 GenerateManualCertificate()
        {
            string subjectName = "CN=localhost"; // 替换为你的主机名或域名
            string friendlyName = "QUIC Test Certificate";
            string exportPassword = "password"; // 导出证书时使用的密码
            string exportFilePath = "quic_test_certificate.pfx"; // 导出的 PFX 文件路径
            
            X509Certificate2 certificate = CreateSelfSignedCertificate(subjectName, friendlyName);
            return certificate;

            byte[] pfxBytes = certificate.Export(X509ContentType.Pfx, exportPassword);
            File.WriteAllBytes(exportFilePath, pfxBytes);
            Console.WriteLine($"证书已创建并导出到 {exportFilePath}");
        }

        static X509Certificate2 CreateSelfSignedCertificate(string subjectName, string friendlyName)
        {
            using (RSA rsa = RSA.Create(2048)) // 使用 2048 位 RSA 密钥
            {
                var certificateRequest = new CertificateRequest(
                    subjectName,
                    rsa,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1
                );

                // 添加扩展属性（如需要）
                certificateRequest.CertificateExtensions.Add(
                    new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, critical: false)
                );

                // 创建证书
                X509Certificate2 certificate = certificateRequest.CreateSelfSigned(
                    notBefore: DateTime.UtcNow.AddDays(-1),
                    notAfter: DateTime.UtcNow.AddYears(1)
                );

                // 设置友好名称
                certificate.FriendlyName = friendlyName;

                return certificate;
            }
        }


        public static X509Certificate2 GenerateManualCertificate1()
        {
            X509Certificate2 cert = null;
            var store = new X509Store("KestrelWebTransportCertificates", StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);
            if (store.Certificates.Count > 0)
            {
                cert = store.Certificates[0];
                if (DateTime.Parse(cert.GetExpirationDateString(), null) < DateTimeOffset.UtcNow)
                {
                    cert = null;
                }
            }

            if (cert == null)
            {
                var now = DateTimeOffset.UtcNow;
                SubjectAlternativeNameBuilder sanBuilder = new();
                sanBuilder.AddDnsName("localhost");
                using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                CertificateRequest req = new("CN=localhost", ec, HashAlgorithmName.SHA256);
                req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection
                {
                    new("1.3.6.1.5.5.7.3.1")

                }, false));

                req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
                req.CertificateExtensions.Add(sanBuilder.Build());
                using var crt = req.CreateSelfSigned(now, now.AddDays(14));
                cert = X509CertificateLoader.LoadCertificate(crt.Export(X509ContentType.Pfx));
                store.Add(cert);
            }

            store.Close();
            var hash = SHA256.HashData(cert.RawData);
            var certStr = Convert.ToBase64String(hash);
            return cert;
        }

    }
}
