using System;
using System.Security.Cryptography;
using System.Text;

namespace Core.Licensing.Signing
{
    /// <summary>
    /// Verifies a license blob against the embedded RSA public key.
    ///
    /// Client-side (ships inside PosSystem.App) — only ever needs the
    /// PUBLIC key XML, so even a fully decompiled app can't forge a valid
    /// license without the private key Baraa holds offline.
    ///
    /// SCOPE NOTE: this class only checks "was this blob really signed by
    /// us, and is it well-formed" — it does NOT check hardware match,
    /// expiry, or clock rollback. Those checks belong to a higher-level
    /// LicenseValidator (Phase 5 in Licensing-Plan.md) that combines this
    /// with FingerprintCollector and the startup validation flow.
    /// </summary>
    public static class LicenseVerifier
    {
        public class VerificationResult
        {
            public bool IsValid { get; private set; }
            public string FailureReason { get; private set; }
            public LicenseData Data { get; private set; }

            public static VerificationResult Success(LicenseData data)
            {
                return new VerificationResult { IsValid = true, Data = data };
            }

            public static VerificationResult Failure(string reason)
            {
                return new VerificationResult { IsValid = false, FailureReason = reason };
            }
        }

        public static VerificationResult Verify(string blob, string publicKeyXml)
        {
            if (string.IsNullOrWhiteSpace(blob))
            {
                return VerificationResult.Failure("License is missing.");
            }
            if (string.IsNullOrWhiteSpace(publicKeyXml))
            {
                throw new ArgumentException("Public key XML is required.", "publicKeyXml");
            }

            string[] parts = blob.Split('.');
            if (parts.Length != 2)
            {
                return VerificationResult.Failure("License file is malformed.");
            }

            byte[] canonicalBytes;
            byte[] signature;
            try
            {
                canonicalBytes = Convert.FromBase64String(parts[0]);
                signature = Convert.FromBase64String(parts[1]);
            }
            catch (FormatException)
            {
                return VerificationResult.Failure("License file is malformed.");
            }

            bool signatureValid;
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.FromXmlString(publicKeyXml);
                signatureValid = rsa.VerifyData(canonicalBytes, "SHA256", signature);
            }

            if (!signatureValid)
            {
                // Deliberately generic message — don't hand a would-be
                // cracker a map of which check failed.
                return VerificationResult.Failure("License is invalid.");
            }

            LicenseData data;
            try
            {
                string canonical = Encoding.UTF8.GetString(canonicalBytes);
                data = LicenseData.FromCanonicalString(canonical);
            }
            catch (FormatException)
            {
                return VerificationResult.Failure("License is invalid.");
            }

            return VerificationResult.Success(data);
        }
    }
}
