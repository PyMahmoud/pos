using System;
using System.Text;

namespace Core.Licensing.Signing
{
    /// <summary>
    /// The data that goes into a signed license: which machine it's for,
    /// when it was issued, when it expires, and an optional admin-facing
    /// note. This is the payload that gets signed — see LicenseSigner /
    /// LicenseVerifier.
    ///
    /// IMPORTANT: HardAnchorHash is HardwareFingerprint.ComputeHardAnchorHash()
    /// — i.e. a hash of CPU ID + motherboard UUID only. Disk serial and MAC
    /// are intentionally never part of the license payload, since they're
    /// soft signals that must never block validation (see Licensing-Plan.md).
    /// </summary>
    public class LicenseData
    {
        private const string FormatTag = "POSLIC1";

        public string HardAnchorHash { get; set; }
        public DateTime IssuedUtc { get; set; }
        public DateTime ExpiresUtc { get; set; }

        /// <summary>Optional admin-facing note (client name, tier, etc).
        /// Never shown to the end user, purely for Baraa/Mahmoud's own
        /// record-keeping when looking at a license file later.</summary>
        public string Note { get; set; }

        /// <summary>
        /// Deterministic pipe-delimited representation. This exact string
        /// (as UTF-8 bytes) is what gets signed — any change to field
        /// order or formatting here breaks verification of every license
        /// already issued, so treat this as a stable wire format once
        /// real licenses exist.
        /// </summary>
        public string ToCanonicalString()
        {
            string noteBase64 = Convert.ToBase64String(
                Encoding.UTF8.GetBytes(Note ?? string.Empty));

            return string.Join("|", new[]
            {
                FormatTag,
                (HardAnchorHash ?? string.Empty).Trim().ToLowerInvariant(),
                IssuedUtc.Ticks.ToString(),
                ExpiresUtc.Ticks.ToString(),
                noteBase64
            });
        }

        public static LicenseData FromCanonicalString(string canonical)
        {
            if (string.IsNullOrEmpty(canonical))
            {
                throw new FormatException("License payload is empty.");
            }

            string[] parts = canonical.Split('|');
            if (parts.Length != 5)
            {
                throw new FormatException("License payload has an unexpected number of fields.");
            }

            if (parts[0] != FormatTag)
            {
                throw new FormatException("License payload has an unrecognized format tag: " + parts[0]);
            }

            long issuedTicks, expiresTicks;
            if (!long.TryParse(parts[2], out issuedTicks) || !long.TryParse(parts[3], out expiresTicks))
            {
                throw new FormatException("License payload has invalid date fields.");
            }

            string note;
            try
            {
                note = Encoding.UTF8.GetString(Convert.FromBase64String(parts[4]));
            }
            catch (FormatException)
            {
                throw new FormatException("License payload has an invalid note field.");
            }

            return new LicenseData
            {
                HardAnchorHash = parts[1],
                IssuedUtc = new DateTime(issuedTicks, DateTimeKind.Utc),
                ExpiresUtc = new DateTime(expiresTicks, DateTimeKind.Utc),
                Note = note
            };
        }
    }
}
