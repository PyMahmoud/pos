using System;
using System.Security.Cryptography;
using System.Text;

namespace Core.Licensing.Fingerprint
{
    /// <summary>
    /// Raw hardware identifiers collected from the local machine, plus the
    /// derived hash used as the license's "hard anchor".
    ///
    /// Matching rule (see Licensing-Plan.md):
    ///   - CpuId and MotherboardUuid are HARD anchors. Both must match the
    ///     license exactly, or the license is rejected.
    ///   - DiskSerial and MacAddress are SOFT signals. They are collected
    ///     and stored for support/debugging only and never block
    ///     validation (a disk or NIC swap should not require a new key).
    /// </summary>
    public class HardwareFingerprint
    {
        public string CpuId { get; set; }
        public string MotherboardUuid { get; set; }
        public string DiskSerial { get; set; }
        public string MacAddress { get; set; }

        /// <summary>
        /// SHA-256 of the two hard anchors only (CpuId + MotherboardUuid).
        /// This is what actually gets embedded and checked in the signed
        /// license blob — never the raw identifiers, and never the soft
        /// signals, so a disk/NIC swap can never affect the hash.
        /// </summary>
        public string ComputeHardAnchorHash()
        {
            string normalizedCpu = Normalize(CpuId);
            string normalizedBoard = Normalize(MotherboardUuid);
            string combined = normalizedCpu + "|" + normalizedBoard;

            using (var sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }

        /// <summary>
        /// Human-readable dump of every raw value, for the activation
        /// screen. Lets Baraa/Mahmoud eyeball a fingerprint before issuing
        /// a key and catch garbage/non-unique board UUIDs (a known risk on
        /// cheap OEM boards — see Licensing-Plan.md) before it becomes a
        /// support problem.
        /// </summary>
        public string ToDisplayString()
        {
            return
                "CPU ID:            " + (CpuId ?? "(unavailable)") + Environment.NewLine +
                "Motherboard UUID:  " + (MotherboardUuid ?? "(unavailable)") + Environment.NewLine +
                "Disk Serial:       " + (DiskSerial ?? "(unavailable)") + " (soft signal, informational only)" + Environment.NewLine +
                "MAC Address:       " + (MacAddress ?? "(unavailable)") + " (soft signal, informational only)" + Environment.NewLine +
                "Hard Anchor Hash:  " + ComputeHardAnchorHash();
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }
            // WMI values on old/cheap hardware sometimes carry stray
            // whitespace or inconsistent casing between reads — normalize
            // so the same physical machine always hashes the same way.
            return value.Trim().ToUpperInvariant();
        }
    }
}
