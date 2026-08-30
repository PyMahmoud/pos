using System;
using System.Data.SQLite;

namespace PosSystem.Core.Data
{
    /// <summary>
    /// Plain key/value store backing PosSystem.App's Settings screen (added
    /// 2026-08-26) — a `settings` table with just Key/Value, created by
    /// DatabaseBootstrapper.EnsureSchema. Deliberately not a typed table
    /// with one column per setting: a handful of loosely-related app-wide
    /// preferences (tax rate, low-stock threshold, and whatever gets added
    /// next) is exactly the case a generic key/value table fits better than
    /// a schema migration per new setting.
    ///
    /// Everything is stored as TEXT and parsed on read — SQLite is
    /// dynamically typed per-cell anyway, and this keeps every value (a
    /// percentage, a quantity, a future on/off flag or free-text string)
    /// going through the exact same two methods rather than one column
    /// per data type.
    /// </summary>
    public class Settings
    {
        private readonly Server server = new Server();

        public string GetString(string key, string defaultValue)
        {
            using (var conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand("SELECT Value FROM settings WHERE Key = @key", conn))
                {
                    cmd.Parameters.AddWithValue("@key", key);
                    object result = cmd.ExecuteScalar();
                    return result == null || result == DBNull.Value ? defaultValue : result.ToString();
                }
            }
        }

        public double GetDouble(string key, double defaultValue)
        {
            string raw = GetString(key, null);
            return raw != null && double.TryParse(raw, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double value)
                ? value
                : defaultValue;
        }

        public void SetString(string key, string value)
        {
            // INSERT OR REPLACE keyed on the PRIMARY KEY column (Key) —
            // simplest way to express "upsert" in SQLite without a separate
            // exists-check-then-insert-or-update round trip.
            using (var conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(
                    "INSERT OR REPLACE INTO settings (Key, Value) VALUES (@key, @value)", conn))
                {
                    cmd.Parameters.AddWithValue("@key", key);
                    cmd.Parameters.AddWithValue("@value", value ?? "");
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void SetDouble(string key, double value) =>
            SetString(key, value.ToString(System.Globalization.CultureInfo.InvariantCulture));

        // Added alongside Settings' new Access Control section (per
        // Mahmoud's request) -- five on/off flags for which admin-gated
        // areas actually prompt for the password. Same TEXT-column,
        // parse-on-read approach as GetDouble/SetDouble above.
        public bool GetBool(string key, bool defaultValue)
        {
            string raw = GetString(key, null);
            if (raw == null) return defaultValue;
            return raw == "1" || raw.Equals("true", System.StringComparison.OrdinalIgnoreCase);
        }

        public void SetBool(string key, bool value) => SetString(key, value ? "1" : "0");
    }
}
