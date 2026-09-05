using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Core.Licensing.Storage
{
    /// <summary>
    /// Reads/writes the signed license blob (see Signing/LicenseSigner,
    /// Signing/LicenseVerifier) to disk, encrypted at rest with Windows
    /// DPAPI (machine scope).
    ///
    /// Why DPAPI on top of the RSA signature: the signature already
    /// guarantees the license can't be forged or edited without detection
    /// — DPAPI adds a SEPARATE, independent protection: a license.dat
    /// copied off this machine onto another machine cannot be decrypted
    /// at all, because DataProtectionScope.LocalMachine ties the
    /// encryption key to this specific Windows installation. That's a
    /// second hardware-binding layer, on top of (not instead of) the
    /// CPU ID + motherboard UUID check that happens after decryption.
    ///
    /// Stored at Documents\PosSystem\license.dat — same folder the
    /// database lives in (see PosSystem.Core.Data.Server), for the same
    /// reason: visible to a non-technical shop owner, survives an app
    /// update/uninstall, still per-user writable. Core.Licensing
    /// deliberately does not reference PosSystem.Core, so the folder name
    /// is duplicated here rather than shared — keep the two constants
    /// in sync if the database location ever moves again.
    /// </summary>
    public static class LicenseStorage
    {
        private const string FolderName = "PosSystem";
        private const string FileName = "license.dat";

        public static string Location
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    FolderName);
            }
        }

        public static string FullPath
        {
            get { return Path.Combine(Location, FileName); }
        }

        public static bool Exists()
        {
            return File.Exists(FullPath);
        }

        /// <summary>
        /// Encrypts and writes the signed license blob. Throws on I/O
        /// failure (permissions, disk full, etc.) — this is only ever
        /// called from an explicit "activate this license" action, so the
        /// caller should catch and show the user a real error rather than
        /// have this fail silently.
        /// </summary>
        public static void Save(string signedBlob)
        {
            if (string.IsNullOrWhiteSpace(signedBlob))
            {
                throw new ArgumentException("License blob is empty.", "signedBlob");
            }

            Directory.CreateDirectory(Location);

            byte[] plainBytes = Encoding.UTF8.GetBytes(signedBlob);
            byte[] encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.LocalMachine);

            File.WriteAllBytes(FullPath, encryptedBytes);
        }

        /// <summary>
        /// Reads and decrypts the stored license blob. Returns null if no
        /// license file exists, or if it exists but can't be decrypted —
        /// the latter covers both a genuinely corrupted file AND a
        /// license.dat copied over from a different machine (DPAPI
        /// LocalMachine scope makes that fail by design). Callers treat
        /// null the same as "no license" either way; they don't need to
        /// (and for security reasons, shouldn't) distinguish the two.
        /// </summary>
        public static string Load()
        {
            if (!File.Exists(FullPath))
            {
                return null;
            }

            try
            {
                byte[] encryptedBytes = File.ReadAllBytes(FullPath);
                byte[] plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.LocalMachine);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (CryptographicException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
        }

        /// <summary>
        /// Clock-rollback guard support (see Validation/LicenseValidator).
        /// Every successful validation records "now" here; a future
        /// validation whose system clock reads earlier than this stored
        /// value (beyond a small tolerance for normal NTP drift) is
        /// treated as a sign the clock was wound back to dodge expiry.
        ///
        /// Stored in its own DPAPI-encrypted file, separate from
        /// license.dat, so touching one never risks corrupting the other.
        /// </summary>
        private const string LastSeenFileName = "license.lastseen";

        private static string LastSeenFullPath
        {
            get { return Path.Combine(Location, LastSeenFileName); }
        }

        public static void SaveLastSeenUtc(DateTime utcNow)
        {
            Directory.CreateDirectory(Location);

            byte[] plainBytes = Encoding.UTF8.GetBytes(utcNow.Ticks.ToString());
            byte[] encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.LocalMachine);

            File.WriteAllBytes(LastSeenFullPath, encryptedBytes);
        }

        public static DateTime? LoadLastSeenUtc()
        {
            if (!File.Exists(LastSeenFullPath))
            {
                return null;
            }

            try
            {
                byte[] encryptedBytes = File.ReadAllBytes(LastSeenFullPath);
                byte[] plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.LocalMachine);
                long ticks;
                if (!long.TryParse(Encoding.UTF8.GetString(plainBytes), out ticks))
                {
                    return null;
                }
                return new DateTime(ticks, DateTimeKind.Utc);
            }
            catch (CryptographicException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
        }
    }
}
