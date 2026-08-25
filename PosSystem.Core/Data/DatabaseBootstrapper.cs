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

                // Added for Inventory's Add Product feature: barcode is
                // optional on a product, but two DIFFERENT products must
                // never share the same non-empty barcode. A partial unique
                // index is the right tool here — SQLite has supported
                // partial indexes since 3.8.0, and the WHERE clause means
                // any number of products can have Barcode = '' (no
                // barcode) while still enforcing true uniqueness for every
                // barcode that IS set. This is enforced at the DB level in
                // addition to the app-level check in Goods.BarcodeExists
                // (belt-and-suspenders — the app check gives a clean error
                // message; this index is the real guarantee).
                //
                // Wrapped in try/catch deliberately: if any existing rows
                // in this database already have duplicate barcodes (data
                // entered before this feature existed, with nothing to
                // prevent it), creating the index would fail with a
                // constraint violation. This method runs on every app
                // startup, so a failure here must never be allowed to
                // crash the app — worst case, the DB-level guarantee is
                // silently skipped and only the app-level check applies.
                try
                {
                    using (var cmd = new SQLiteCommand(
                        @"CREATE UNIQUE INDEX IF NOT EXISTS idx_goods_barcode_unique
                          ON goods(Barcode)
                          WHERE Barcode IS NOT NULL AND Barcode <> ''", conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
                catch (SQLiteException)
                {
                    // Pre-existing duplicate barcodes (or some other
                    // constraint issue) — see comment above. Not fatal.
                }

                // Added for Inventory's category management feature
                // (create/delete a category as its own action, independent
                // of adding a product). Categories were previously only
                // ever implicit — whatever distinct strings happened to be
                // on goods.Category — with no way to know about a category
                // that has zero products yet, and no safe way to delete
                // one. This is a real, minimal table now: just a unique
                // name, nothing else (the legacy Data/Categories.cs class
                // predates this and assumes a richer `categories` table
                // with Type/Image columns from the old pre-rebuild schema —
                // that table was never actually created by anything in
                // this rebuild, so those older methods were dead code
                // against a nonexistent table until this CREATE TABLE;
                // left as-is/unused rather than touched, since nothing
                // depends on removing them — the new methods added
                // alongside them on that same class are what Inventory
                // actually calls now).
                //
                // COLLATE NOCASE on the UNIQUE constraint matches how every
                // other category comparison in this app already works
                // (StringComparison.OrdinalIgnoreCase in InventoryViewModel)
                // — "Snacks" and "snacks" are the same category, not two.
                using (var cmd = new SQLiteCommand(
                    @"CREATE TABLE IF NOT EXISTS categories (
                        ID INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT UNIQUE NOT NULL COLLATE NOCASE
                    )", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // One-time backfill: every category already in use by an
                // existing product becomes a known category immediately,
                // so nothing already on a product's card breaks the new
                // "category must be selected from the known list" rule
                // Inventory's Add Product / Edit Product forms now enforce.
                // INSERT OR IGNORE makes this safe to run on every startup
                // — a no-op once every existing category has been migrated.
                //
                // Wrapped in try/catch for the same reason as the barcode
                // index above, and a real possibility here specifically: if
                // a `categories` table already existed in this on-disk .db
                // from the pre-rebuild app (the legacy Data/Categories.cs
                // class assumes one, with Type/Image columns and no UNIQUE
                // constraint on Name), CREATE TABLE IF NOT EXISTS just now
                // silently left that older schema in place rather than
                // creating this file's intended one — this session has no
                // way to inspect the actual live .db schema to confirm
                // either way. If that's the case here, this INSERT still
                // works fine (Name is the only column every version of this
                // table has), but OR IGNORE has nothing to de-duplicate
                // against without the UNIQUE constraint, so — belt-and-
                // suspenders, matching Goods.BarcodeExists' app-level check
                // alongside its own DB-level index — CategoryExists() in
                // Data/Categories.cs (case-insensitive) is what actually
                // guarantees no duplicate category through this app's own
                // Add-category flow, regardless of which schema is really
                // on disk.
                try
                {
                    using (var cmd = new SQLiteCommand(
                        @"INSERT OR IGNORE INTO categories (Name)
                          SELECT DISTINCT Category FROM goods
                          WHERE Category IS NOT NULL AND TRIM(Category) <> ''", conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
                catch (SQLiteException)
                {
                    // Pre-existing categories table with a stricter/older
                    // schema (e.g. a NOT NULL Type/Image column with no
                    // default) -- see comment above. Not fatal; the app-level
                    // CategoryExists check is the real guarantee either way.
                }

                // One-time de-duplication pass (2026-08-25): this database
                // already had duplicate/case-variant rows in `categories`
                // before this feature's UNIQUE COLLATE NOCASE constraint
                // existed (confirmed from a live screenshot showing the same
                // category name repeated 2-3x in every picker) -- likely
                // from the backfill above running before that constraint
                // was added, or from a pre-existing categories table (see
                // the CREATE TABLE comment above) that never had it at all.
                // Categories.ReadAllCategoryNames now also folds duplicates
                // at query time regardless, but leaving the underlying rows
                // duplicated forever would still be wrong for anything else
                // that ever reads this table directly, and it means Delete
                // Category only fully removes a name if this cleanup ran.
                // Keeps the lowest ID per case-insensitive name and removes
                // the rest -- safe to re-run every startup (a no-op once
                // there is nothing left to de-duplicate), and goods.Category
                // is a plain string column, not a foreign key to a specific
                // categories.ID, so deleting the extra duplicate ID rows
                // here has no effect on any product's own Category value.
                try
                {
                    using (var cmd = new SQLiteCommand(
                        @"DELETE FROM categories
                          WHERE ID NOT IN (
                              SELECT MIN(ID) FROM categories GROUP BY Name COLLATE NOCASE
                          )", conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
                catch (SQLiteException)
                {
                    // Same pre-existing-schema caveat as the two try/catch
                    // blocks above -- not fatal either way.
                }
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
