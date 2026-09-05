using System;
using Core.Licensing.Fingerprint;
using Core.Licensing.Signing;
using Core.Licensing.Storage;

namespace Core.Licensing.Validation
{
    /// <summary>
    /// Result of a full license validation pass — signature, hardware
    /// match, expiry, and clock-rollback all checked together.
    /// </summary>
    public class LicenseValidationResult
    {
        public LicenseStatus Status { get; private set; }
        public string FriendlyMessage { get; private set; }
        public LicenseData Data { get; private set; }

        public bool IsValid
        {
            get { return Status == LicenseStatus.Valid; }
        }

        internal LicenseValidationResult(LicenseStatus status, string friendlyMessage, LicenseData data)
        {
            Status = status;
            FriendlyMessage = friendlyMessage;
            Data = data;
        }
    }

    /// <summary>
    /// Combines FingerprintCollector + LicenseVerifier + LicenseStorage
    /// into the single check the app runs at startup. See
    /// Licensing-Plan.md, "Validation logic" section, for the checklist
    /// this implements step by step.
    ///
    /// Deliberately generic FriendlyMessage on every failure branch
    /// except Missing (which is a normal first-run state, not a failure)
    /// — per the plan, the app should never hand a would-be cracker a
    /// breakdown of exactly which check failed.
    /// </summary>
    public static class LicenseValidator
    {
        /// <summary>
        /// Tolerance before a clock that reads earlier than last-seen is
        /// treated as a rollback attempt rather than normal drift (NTP
        /// sync, timezone/DST changes, etc). Generous on purpose: a false
        /// positive here locks out a paying, honest client.
        /// </summary>
        private static readonly TimeSpan ClockRollbackTolerance = TimeSpan.FromHours(6);

        private const string GenericInvalidMessage =
            "This installation's license is invalid or has expired. Please contact support.";

        public static LicenseValidationResult ValidateStoredLicense()
        {
            string blob = LicenseStorage.Load();
            return Validate(blob);
        }

        public static LicenseValidationResult Validate(string blob)
        {
            if (string.IsNullOrWhiteSpace(blob))
            {
                return new LicenseValidationResult(
                    LicenseStatus.Missing,
                    "This installation hasn't been activated yet.",
                    null);
            }

            LicenseVerifier.VerificationResult verification = LicenseVerifier.Verify(blob, LicensePublicKey.Xml);
            if (!verification.IsValid)
            {
                return new LicenseValidationResult(LicenseStatus.Invalid, GenericInvalidMessage, null);
            }

            LicenseData data = verification.Data;

            HardwareFingerprint fingerprint = FingerprintCollector.Collect();
            string currentHash = fingerprint.ComputeHardAnchorHash();
            string licensedHash = (data.HardAnchorHash ?? string.Empty).Trim().ToLowerInvariant();

            if (!string.Equals(currentHash, licensedHash, StringComparison.Ordinal))
            {
                return new LicenseValidationResult(LicenseStatus.HardwareMismatch, GenericInvalidMessage, data);
            }

            DateTime nowUtc = DateTime.UtcNow;
            DateTime? lastSeenUtc = LicenseStorage.LoadLastSeenUtc();

            if (lastSeenUtc.HasValue && nowUtc < lastSeenUtc.Value - ClockRollbackTolerance)
            {
                return new LicenseValidationResult(LicenseStatus.ClockRollbackDetected, GenericInvalidMessage, data);
            }

            if (nowUtc > data.ExpiresUtc)
            {
                return new LicenseValidationResult(LicenseStatus.Expired, GenericInvalidMessage, data);
            }

            // Every check passed — record this moment so a future clock
            // rollback has to beat it, not the license's original issue date.
            LicenseStorage.SaveLastSeenUtc(nowUtc);

            return new LicenseValidationResult(LicenseStatus.Valid, "License is valid.", data);
        }
    }
}
