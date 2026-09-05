namespace Core.Licensing.Validation
{
    /// <summary>
    /// Outcome of a license validation attempt. Kept deliberately generic
    /// in the enum names/messages presented to the end user (see
    /// LicenseValidator) — a would-be cracker probing the app shouldn't
    /// be able to tell hardware-mismatch from expiry from tampering just
    /// by reading the error text.
    /// </summary>
    public enum LicenseStatus
    {
        Valid,
        Missing,
        Invalid,
        HardwareMismatch,
        Expired,
        ClockRollbackDetected
    }
}
