using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
namespace AKNet.Common
{
    //.cer / .crt 文件内容：.cer 或.crt 文件通常只包含证书本身（即公钥和证书信息），不包含私钥。
    //.pfx 文件（也称为 .p12 文件）是一个容器，可以包含以下内容：证书链：包括服务器证书、中间证书和根证书。私钥：与证书对应的私钥，用于身份验证。
    //.pfx（Personal Information Exchange）格式是一种二进制格式，用于存储证书及其私钥
    //PEM（Privacy Enhanced Mail）格式是一种基于 Base64 编码的文本格式，用于存储和传输加密材料（如证书、私钥等）。
    internal static class X509CertTool
    {
        private const string Password = "123456"; // 导出证书时使用的密码
        private const string storeName = "xuke_quic_test_cert";
        private const string pem_fileName = "xuke_quic_test_cert.pem";
        private const string pfx_fileName = "xuke_quic_test_cert.pfx";
        private const string cert_fileName = "xuke_quic_test_cert.cert";

        public static X509Certificate2 GetCert()
        {

            X509Certificate2 ori_X509Certificate2 = GetCertFromX509Store();
            if (ori_X509Certificate2 == null)
            {
                ori_X509Certificate2 = CreateCert();
            }
            X509Certificate2 new_X509Certificate2 = LoadCert(ori_X509Certificate2);
            NetLog.Log("11111111111111111111111111");
            return new_X509Certificate2;
        }

        public static X509Certificate2 GetCertFromX509Store()
        {
            X509Certificate2 ori_X509Certificate2 = null;
            X509Store mX509Store = new X509Store(storeName, StoreLocation.CurrentUser);
            mX509Store.Open(OpenFlags.ReadWrite);
            while (mX509Store.Certificates.Count > 1)
            {
                mX509Store.Certificates.RemoveAt(0);
            }

            if (mX509Store.Certificates.Count == 1)
            {
                var cert = mX509Store.Certificates[0];
                if (orCertValid(cert))
                {
                    ori_X509Certificate2 = cert;
                }
            }
            mX509Store.Close();
            return ori_X509Certificate2;
        }

        static bool orCertValid(X509Certificate2 certificate)
        {
            if (DateTime.Now < certificate.NotBefore || DateTime.Now > certificate.NotAfter)
            {
                NetLog.LogError("证书已过期或尚未生效！");
                return false;
            }

            // 验证证书链
            X509Chain chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck; // 可以根据需要启用吊销检查
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
            bool isValid = chain.Build(certificate);
            if (!isValid)
            {
                NetLog.LogError("证书链验证失败！");
                foreach (var status in chain.ChainStatus)
                {
                    NetLog.LogError("错误信息: " + status.StatusInformation);
                }
            }
            return true;
        }

        static void SaveCert_Pem(X509Certificate2 certificate)
        {
            // 获取证书的 DER 编码字节
            byte[] derBytes = certificate.Export(X509ContentType.Cert, Password);
            // 将 DER 编码字节转换为 PEM 格式
            const string header = "-----BEGIN CERTIFICATE-----";
            const string footer = "-----END CERTIFICATE-----";
            string base64 = Convert.ToBase64String(derBytes);
            string pem = $"{header}\n{base64}\n{footer}";
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, pem_fileName), pem);
        }

        static X509Certificate2 CreateCert()
        {
            string subjectName = "CN=localhost"; // 替换为你的主机名或域名
            string friendlyName = "quic_test_cert";
            X509Certificate2 certificate = CreateSelfSignedCertificate(subjectName, friendlyName);
            if (orCertValid(certificate))
            {
                X509Store mX509Store = new X509Store(storeName, StoreLocation.CurrentUser);
                mX509Store.Open(OpenFlags.ReadWrite);
                mX509Store.Add(certificate);
                mX509Store.Close();

                SaveCert_Pem(certificate);
                return certificate;
            }
            return null;
        }

        static X509Certificate2 LoadCert(X509Certificate2 ori_cert)
        {
            byte[] derBytes = ori_cert.Export(X509ContentType.Cert, Password);
            return X509CertificateLoader.LoadCertificate(derBytes);
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
    }
}
