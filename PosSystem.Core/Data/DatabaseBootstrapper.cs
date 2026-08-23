using System;
using System.Data.SQLite;

namespace PosSystem.Core.Data
{
    /// <summary>
    /// Ensures schema additions needed by newer features exist, without
    /// requiring a fresh rovaShop.db or a manual migration step. Runs
    /// idempotent CREATE TABLE IF NOT EXISTS / ALTER TABLE ADD COLUMN
    /// checks — safe to call on every app startup, cheap enough (a couple
    /// of PRAGMA queries) that it doesn't need to be conditional on
    /// first-run.
    ///
    /// Added for the pharma-distributor stock-check feature:
    /// - `stockchecks` table (new): a full history log of what a rep found
    ///   on-hand at a customer's pharmacy on each visit — quantity,
    ///   batch/lot, and expiry per the client's stated requirement.
    /// - `bills.CustomerId` (new column): Bills previously only stored a
    ///   denormalized snapshot of the customer's name/ID/phone at sale time
    ///   (Ownername/Ownerid/Ownernumber) — fine for a receipt, but fragile
    ///   to join on reliably (a re-typed name breaks the match). A real
    ///   integer FK makes "every sale to customer X" correct instead of
    ///   best-effort string matching.
    ///
    /// Call this once, early, before any screen touches the database — see
    /// App.xaml.cs.
    /// </summary>
    public static class DatabaseBootstrapper
    {
        public static void EnsureSchema()
        {
            var server = new Server();

            using (var conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();

                using (var cmd = new SQLiteCommand(
                    @"CREATE TABLE IF NOT EXISTS stockchecks (
                        ID INTEGER PRIMARY KEY AUTOINCREMENT,
                        CustomerId INTEGER,
                        GoodBarcode TEXT,
                        MedicationName TEXT,
                        Quantity REAL,
                        BatchNumber TEXT,
                        ExpiryDate TEXT,
                        CheckDate TEXT,
                        CheckTime TEXT,
                        Notes TEXT
                    )", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                EnsureColumn(conn, "bills", "CustomerId", "INTEGER");
            }
        }

        private static void EnsureColumn(SQLiteConnection conn, string table, string column, string sqlType)
        {
            bool exists = false;
            using (var cmd = new SQLiteCommand($"PRAGMA table_info({table})", conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (string.Equals(reader["name"].ToString(), column, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }
            }

            if (!exists)
            {
                using (var cmd = new SQLiteCommand($"ALTER TABLE {table} ADD COLUMN {column} {sqlType}", conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
