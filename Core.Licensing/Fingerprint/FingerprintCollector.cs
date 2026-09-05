using System;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;

namespace Core.Licensing.Fingerprint
{
    /// <summary>
    /// Reads hardware identifiers from the local machine via WMI (CPU,
    /// motherboard, disk) and .NET networking APIs (MAC).
    ///
    /// Every individual lookup is wrapped so a single missing/unsupported
    /// WMI class on old or unusual hardware degrades that one field to
    /// "(unavailable)" instead of throwing and blocking activation
    /// entirely. CpuId and MotherboardUuid are still required to be
    /// present for a license to actually validate later (see
    /// LicenseValidator, added in a later phase) — this class only
    /// collects, it doesn't decide pass/fail.
    /// </summary>
    public static class FingerprintCollector
    {
        public static HardwareFingerprint Collect()
        {
            return new HardwareFingerprint
            {
                CpuId = GetCpuId(),
                MotherboardUuid = GetMotherboardUuid(),
                DiskSerial = GetDiskSerial(),
                MacAddress = GetMacAddress()
            };
        }

        private static string GetCpuId()
        {
            return QuerySingleWmiValue("SELECT ProcessorId FROM Win32_Processor", "ProcessorId");
        }

        private static string GetMotherboardUuid()
        {
            // Win32_ComputerSystemProduct.UUID is the standard SMBIOS
            // system UUID and is what's normally meant by "motherboard
            // UUID" in practice. Known risk on cheap OEM boards: this can
            // come back as all zeros or duplicated across a manufacturing
            // batch — flagged in Licensing-Plan.md, surfaced via
            // ToDisplayString() so it can be sanity-checked before a key
            // is issued.
            string uuid = QuerySingleWmiValue("SELECT UUID FROM Win32_ComputerSystemProduct", "UUID");

            if (IsKnownBadUuid(uuid))
            {
                return uuid; // still return it — display layer flags it, we don't silently substitute
            }

            return uuid;
        }

        private static string GetDiskSerial()
        {
            // Soft signal only — first physical disk (index 0) is enough,
            // this is never a hard match requirement.
            return QuerySingleWmiValue(
                "SELECT SerialNumber FROM Win32_DiskDrive WHERE Index = 0",
                "SerialNumber");
        }

        private static string GetMacAddress()
        {
            try
            {
                var nic = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up)
                    .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                    .OrderByDescending(n => n.Speed) // prefer the "real" NIC over virtual adapters
                    .FirstOrDefault();

                if (nic == null)
                {
                    return null;
                }

                byte[] bytes = nic.GetPhysicalAddress().GetAddressBytes();
                if (bytes == null || bytes.Length == 0)
                {
                    return null;
                }

                return string.Join(":", bytes.Select(b => b.ToString("X2")));
            }
            catch
            {
                return null;
            }
        }

        private static string QuerySingleWmiValue(string query, string propertyName)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(query))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        object value = obj[propertyName];
                        if (value != null)
                        {
                            string s = value.ToString();
                            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
                        }
                    }
                }
            }
            catch
            {
                // WMI class/property unsupported on this machine — degrade
                // to null rather than throwing, per class-level comment.
            }

            return null;
        }

        private static bool IsKnownBadUuid(string uuid)
        {
            if (string.IsNullOrWhiteSpace(uuid))
            {
                return true;
            }

            string normalized = uuid.Trim().ToUpperInvariant();
            return normalized == "00000000-0000-0000-0000-000000000000"
                || normalized == "FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF"
                || normalized.Replace("-", "").All(c => c == '0');
        }
    }
}
