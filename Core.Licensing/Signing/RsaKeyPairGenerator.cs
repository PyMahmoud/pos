using System.Security.Cryptography;

namespace Core.Licensing.Signing
{
    /// <summary>
    /// Generates RSA keypairs for license signing. This is an admin-only
    /// operation — run once (or occasionally, if the key is ever rotated)
    /// on Baraa/Mahmoud's own machine via the key-generation tool (Phase C
    /// in Licensing-Plan.md), never on a client machine.
    ///
    /// The resulting PRIVATE key XML must never be embedded in
    /// PosSystem.App or committed to the repo in plaintext — store it
    /// somewhere offline/secure and paste it into the admin tool only when
    /// generating client keys. Only the PUBLIC key XML is meant to be
    /// embedded in the shipped client app (see LicenseVerifier).
    /// </summary>
    public static class RsaKeyPairGenerator
    {
        /// <summary>
        /// 3072 bits: comfortable long-term security margin. Cost is paid
        /// once at signing time on a normal dev machine — verification on
        /// old client hardware is a single operation at app startup and
        /// stays fast regardless of key size at this range.
        /// </summary>
        public const int DefaultKeySizeBits = 3072;

        public static void Generate(out string publicKeyXml, out string privateKeyXml, int keySizeBits = DefaultKeySizeBits)
        {
            using (var rsa = new RSACryptoServiceProvider(keySizeBits))
            {
                // true = include private parameters
                privateKeyXml = rsa.ToXmlString(true);
                // false = public parameters only
                publicKeyXml = rsa.ToXmlString(false);
            }
        }
    }
}
