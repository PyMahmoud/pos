namespace Core.Licensing.Signing
{
    /// <summary>
    /// The RSA PUBLIC key used to verify signed licenses. Safe to ship
    /// inside PosSystem.App — even a fully decompiled client app only
    /// ever has this, never the private key, so it can't be used to forge
    /// a license.
    ///
    /// The matching PRIVATE key lives at the repo root
    /// (PrivateSigningKey.xml, generated 2026-09-04) — used only by the
    /// admin key-generation tool, never referenced from here or from
    /// PosSystem.App.
    /// </summary>
    public static class LicensePublicKey
    {
        public const string Xml =
            "<RSAKeyValue><Modulus>6HNF+rt9gQWR14Ki17QL6zkeI91Qt4JV1rQNuOx2g56QQNyNZyToh7i7+RpG44e3qzT69QTcSZwItiILO4CnqKxh5kubmIhPM2Ss5krkrAX+ErgcmotWdC5Dkvtrx9JfbPKvF98Po5YP30wnjZqODHxoW4vGZRInlDc7UnxtuHiX/7FCQBHiyYZAWzjMM90gG4w87Bff+ujasRqeDOrVvfuGTkjdmGaXYqyW09jV/dcfmXu8TS+o1Vcr55bh1+zK+HP9hhRLjpZJFz+0pfKsSqiz4ZQK5SWrOkJvYGQ1prhWN85RyMfvVoXYiyNKU24jpZl8Sxzt2fgGa4abL7qic9lq2rdvYZkc1BcoMyofOwmwhjmS4qT0B/BT/aOuuw0Ka+U4n9qzrph9kQCh+8OrcAm36/W978zaAr0ovIBNizu/3++hI+9dxyDyvUUA9CGeEx2tCfNBoIwlf5ZXC2SxPoGZP+zLuR/sKFrDZJVRtDOTaNGfz/Glf/d3SS2aRpg3</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";
    }
}
