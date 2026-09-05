using System;
using System.Security.Cryptography;
using System.Text;

namespace Core.Licensing.Signing
{
    /// <summary>
    /// Signs a LicenseData payload with an RSA private key, producing a
    /// portable license blob string.
    ///
    /// Admin-side only — this needs the PRIVATE key XML, which must never
    /// ship inside PosSystem.App. This class is meant to be called from
    /// the separate admin key-generation tool (Phase C), not from the POS
    /// app itself.
    ///
    /// Blob format: "{base64(canonical payload bytes)}.{base64(signature)}"
    /// — deliberately simple (no JSON/XML wrapper) so it's easy to eyeball,
    /// copy into an email, or paste into a text file by hand.
    /// </summary>
    public static class LicenseSigner
    {
        public static string Sign(LicenseData data, string privateKeyXml)
        {
            if (data == null) throw new ArgumentNullException("data");
            if (string.IsNullOrWhiteSpace(privateKeyXml)) throw new ArgumentException("Private key XML is required.", "privateKeyXml");

            byte[] canonicalBytes = Encoding.UTF8.GetBytes(data.ToCanonicalString());

            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.FromXmlString(privateKeyXml);
                byte[] signature = rsa.SignData(canonicalBytes, "SHA256");

                return Convert.ToBase64String(canonicalBytes) + "." + Convert.ToBase64String(signature);
            }
        }
    }
}
