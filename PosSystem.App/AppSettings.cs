using System;
using System.Security.Cryptography;
using System.Text;

namespace PosSystem.App
{
    /// <summary>
    /// Static, app-wide settings — added 2026-08-26 alongside the Settings
    /// screen actually getting content. Backed by Core.Data.Settings (a
    /// plain key/value table), loaded once at startup (App.xaml.cs, right
    /// after DatabaseBootstrapper.EnsureSchema — same ordering reason:
    /// nothing should read these before the table backing them is known to
    /// exist) and re-loaded by SettingsViewModel after every save.
    ///
    /// Two settings, both explicitly flagged elsewhere in this codebase as
    /// "should be a real setting, not a guess" before this existed:
    ///
    /// - TaxRatePercent: CheckoutViewModel.Total was hardcoded to Subtotal
    ///   (tax always 0) with a comment pointing here — "Tax/Discount are
    ///   Settings-driven... not invented here." This is that.
    /// - LowStockThreshold: InventoryRow.LowStockThreshold was a plain
    ///   constant (10) with a comment explaining it was a placeholder
    ///   default, not confirmed with the client, and would become a real
    ///   setting "if the client wants this configurable... a real
    ///   follow-up, not a guess made now." This is that follow-up.
    ///
    /// Static rather than an instance owned by one ViewModel: several
    /// screens that don't otherwise share a ViewModel each need to read
    /// these (Checkout for tax, Inventory for the stock badges), and
    /// they're genuinely app-wide/global in the same sense the active
    /// theme and language already are (ThemeManager, LocalizationManager —
    /// both also static, both also fire a Changed event on swap, same
    /// pattern followed here).
    /// </summary>
    public static class AppSettings
    {
        private const string TaxRateKey = "TaxRatePercent";
        private const string LowStockKey = "LowStockThreshold";
        private const string AdminPasswordHashKey = "AdminPasswordHash";

        // Access Control (added per Mahmoud's request) -- which of the
        // admin-gated areas actually prompt for the password. A password
        // can be set (HasAdminPassword true) while individual areas are
        // switched off here -- e.g. keep Dashboard locked but leave
        // Inventory open to any staff member. Deliberately does NOT include
        // a flag for the Admin Password section or this Access Control
        // section itself -- see SettingsViewModel.IsAccessControlUnlocked's
        // doc comment for why those two stay permanently gated whenever a
        // password is set, rather than being switches that could disable
        // their own protection.
        private const string GateDashboardKey = "GateDashboardEnabled";
        private const string GateInventoryKey = "GateInventoryEnabled";
        private const string GateBillsKey = "GateBillsEnabled";
        private const string GateSettingsPreferencesKey = "GateSettingsPreferencesEnabled";
        private const string GateSettingsExportKey = "GateSettingsExportEnabled";

        // Same defaults each setting effectively had before this existed:
        // 0% tax (Checkout's old hardcoded 0), and 10 units (InventoryRow's
        // old constant).
        public static double TaxRatePercent { get; private set; } = 0;
        public static double LowStockThreshold { get; private set; } = 10;

        // Admin password (#7, 2026-08-27) — gates Dashboard access (revenue
        // is business-sensitive) and is meant to extend to product/category
        // add-edit-delete next, per Mahmoud's stated scope. Stored as a
        // SHA-256 hash, never the plain password — this is a basic deterrent
        // against someone casually opening rovaShop.db in a SQLite browser,
        // not a claim of real security (no salt, no iteration count/KDF like
        // PBKDF2 or bcrypt) — proportionate to what this app actually needs
        // protected (a shop's own revenue numbers, not customer payment
        // data), but flagging the tradeoff rather than presenting it as more
        // than it is.
        public static bool HasAdminPassword { get; private set; }

        // Access Control flags -- see the matching Key consts above. All
        // five default true, matching the hardcoded "gated whenever a
        // password is set" behavior every one of these screens had before
        // this setting existed, so a fresh install/upgrade changes nothing
        // until someone actually visits Settings' new Access Control
        // section and turns something off.
        public static bool GateDashboardEnabled { get; private set; } = true;
        public static bool GateInventoryEnabled { get; private set; } = true;
        public static bool GateBillsEnabled { get; private set; } = true;
        public static bool GateSettingsPreferencesEnabled { get; private set; } = true;
        public static bool GateSettingsExportEnabled { get; private set; } = true;

        /// <summary>
        /// Fired after a successful save (SettingsViewModel) so any
        /// already-open screen updates immediately — a cashier mid-shift on
        /// Checkout shouldn't need to restart the app for a tax-rate change
        /// made on Settings to apply to their next sale.
        /// </summary>
        public static event Action Changed;

        public static void Load()
        {
            var settings = new Core.Data.Settings();
            TaxRatePercent = settings.GetDouble(TaxRateKey, 0);
            LowStockThreshold = settings.GetDouble(LowStockKey, 10);
            HasAdminPassword = !string.IsNullOrEmpty(settings.GetString(AdminPasswordHashKey, ""));

            GateDashboardEnabled = settings.GetBool(GateDashboardKey, true);
            GateInventoryEnabled = settings.GetBool(GateInventoryKey, true);
            GateBillsEnabled = settings.GetBool(GateBillsKey, true);
            GateSettingsPreferencesEnabled = settings.GetBool(GateSettingsPreferencesKey, true);
            GateSettingsExportEnabled = settings.GetBool(GateSettingsExportKey, true);
        }

        /// <summary>
        /// Sets or changes the admin password. Passing an empty string
        /// clears it (HasAdminPassword becomes false again, un-gating
        /// Dashboard) — an intentional escape hatch: this app has no admin
        /// recovery flow, so if Mahmoud (or the client) forgets the
        /// password, editing this back out via Settings while already
        /// logged into Windows as the machine's normal user is the only way
        /// back in short of clearing the `settings` table row directly in
        /// the .db file.
        /// </summary>
        public static void SetAdminPassword(string plainPassword)
        {
            var settings = new Core.Data.Settings();
            string hash = string.IsNullOrEmpty(plainPassword) ? "" : HashPassword(plainPassword);
            settings.SetString(AdminPasswordHashKey, hash);
            HasAdminPassword = !string.IsNullOrEmpty(hash);
        }

        public static bool VerifyAdminPassword(string attempt)
        {
            if (!HasAdminPassword) return true;
            var settings = new Core.Data.Settings();
            string storedHash = settings.GetString(AdminPasswordHashKey, "");
            return !string.IsNullOrEmpty(attempt) && storedHash == HashPassword(attempt);
        }

        private static string HashPassword(string plainPassword)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(plainPassword));
                var sb = new StringBuilder();
                foreach (byte b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        public static void Save(double taxRatePercent, double lowStockThreshold)
        {
            var settings = new Core.Data.Settings();
            settings.SetDouble(TaxRateKey, taxRatePercent);
            settings.SetDouble(LowStockKey, lowStockThreshold);

            TaxRatePercent = taxRatePercent;
            LowStockThreshold = lowStockThreshold;

            Changed?.Invoke();
        }

        /// <summary>
        /// Saves which admin-gated areas actually prompt for the admin
        /// password (Settings' new Access Control section, added per
        /// Mahmoud's request) -- lets a password exist and stay set while
        /// selectively skipping the prompt for areas this shop doesn't need
        /// gated, without touching the password itself. Deliberately takes
        /// no parameter for the Admin Password section or this Access
        /// Control section itself -- both stay permanently gated whenever a
        /// password is set, not user-configurable (see
        /// SettingsViewModel.IsAccessControlUnlocked's doc comment for why:
        /// making the switch panel itself switchable would let someone
        /// without the password turn off every other switch).
        /// </summary>
        public static void SaveGateSettings(bool dashboard, bool inventory, bool bills, bool settingsPreferences, bool settingsExport)
        {
            var settings = new Core.Data.Settings();
            settings.SetBool(GateDashboardKey, dashboard);
            settings.SetBool(GateInventoryKey, inventory);
            settings.SetBool(GateBillsKey, bills);
            settings.SetBool(GateSettingsPreferencesKey, settingsPreferences);
            settings.SetBool(GateSettingsExportKey, settingsExport);

            GateDashboardEnabled = dashboard;
            GateInventoryEnabled = inventory;
            GateBillsEnabled = bills;
            GateSettingsPreferencesEnabled = settingsPreferences;
            GateSettingsExportEnabled = settingsExport;

            Changed?.Invoke();
        }
    }
}
