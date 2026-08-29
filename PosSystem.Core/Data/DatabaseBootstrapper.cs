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

                // WAL journal mode added 2026-08-28 (bug report: deleting a
                // bill line freezes the app, then shows "database is
                // locked") -- see Server.connectionString's own comment for
                // the full diagnosis. SQLite's default rollback-journal mode
                // requires a writer to briefly hold an exclusive lock that
                // blocks every reader; this app opens many short-lived
                // connections in quick succession for a single user action
                // (BillsBrowserViewModel.DeleteLine alone is ~10, plus
                // whatever Dashboard/Inventory's event-driven refreshes add
                // on top), which is exactly the access pattern rollback-mode
                // locking fights against. WAL lets readers and a writer
                // proceed concurrently instead of blocking each other, which
                // should eliminate the vast majority of these conflicts
                // outright -- BusyTimeout on the connection string is the
                // remaining safety net for whatever WAL doesn't cover (e.g.
                // two writers landing at the same instant).
                //
                // journal_mode=WAL is a PERSISTENT, one-time setting stored
                // in the database file itself (not a per-connection PRAGMA
                // like busy_timeout would be) -- once set, every future
                // connection from any process uses WAL automatically, so
                // running this on every startup is belt-and-suspenders, not
                // repeated work. Query the result rather than assume success:
                // WAL can fail to engage on some network/exotic filesystems,
                // and if that ever happens here, EnsureSchema must not throw
                // over it -- the app still works in the default journal
                // mode, just with more of the original lock-contention risk.
                try
                {
                    using (var cmd = new SQLiteCommand("PRAGMA journal_mode=WAL;", conn))
                    {
                        cmd.ExecuteScalar();
                    }
                }
                catch (SQLiteException)
                {
                    // Not fatal -- see comment above. Falls back to
                    // whichever journal mode the file already had.
                }

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
                // Added for the Settings screen's Tax Rate / Low Stock
                // Threshold fields (2026-08-26) -- see Core.Data.Settings'
                // class doc comment for why this is a generic key/value
                // table rather than one schema column per setting.
                using (var cmd = new SQLiteCommand(
                    @"CREATE TABLE IF NOT EXISTS settings (
                        Key TEXT PRIMARY KEY,
                        Value TEXT
                    )", conn))
                {
                    cmd.ExecuteNonQuery();
                }

                // Added 2026-08-28 for receipt revisioning (Mahmoud's
                // explicit requirement): removing/returning a product from
                // a bill must no longer rewrite that bill's own row in
                // place -- it must leave the original receipt untouched as
                // history and add a NEW bills row carrying the same
                // Billnumber plus a suffix ("210" -> "210-e1", a second
                // return on the same receipt -> "210-e2", etc. -- see
                // BillsBrowserViewModel for where RevisionSuffix values are
                // actually assigned). IsCurrent marks which one of a
                // receipt's rows (there can now be several sharing one
                // Billnumber) is the one that counts toward Dashboard/
                // Customer-balance/Excel-export totals -- every reader of
                // `bills` that computes a KPI or a running balance must
                // filter to IsCurrent = 1, or a returned bill's original,
                // now-superseded totals would double up against its
                // replacement's. RevisionSuffix is NULL for an original,
                // never-edited bill (Core.Models.Bills.DisplayNumber
                // reads that as "no suffix to show").
                EnsureColumn(conn, "bills", "IsCurrent", "INTEGER");
                EnsureColumn(conn, "bills", "RevisionSuffix", "TEXT");

                // ALTER TABLE ADD COLUMN always inserts NULL for existing
                // rows, never a real default (SQLite doesn't apply
                // ADD COLUMN's DEFAULT retroactively the way some other
                // engines do for this specific syntax) -- so every bill
                // that existed before this feature needs an explicit
                // one-time backfill to IsCurrent = 1 ("still the current/
                // only version of this receipt"), or DbNullSafe.ToBool's
                // null-is-true fallback would be silently doing this same
                // job forever instead of the data itself just saying so.
                // Safe to re-run every startup -- a no-op once every row
                // already has a real 0/1.
                try
                {
                    using (var cmd = new SQLiteCommand(
                        "UPDATE bills SET IsCurrent = 1 WHERE IsCurrent IS NULL", conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
                catch (SQLiteException)
                {
                    // Same pre-existing-schema caveat as the categories
                    // backfills above -- not fatal; DbNullSafe.ToBool's own
                    // null-is-true fallback still covers any row this
                    // somehow missed.
                }

                // sells.BillId (new column): the per-revision counterpart
                // to bills.IsCurrent/RevisionSuffix above. Billnumber alone
                // is no longer enough to find "this bill's line items" --
                // once a receipt has been returned-from, MULTIPLE bills
                // rows share the same Billnumber (the original plus every
                // revision), so a Sells row also needs to say which SPECIFIC
                // bills.ID row it belongs to, not just which receipt number.
                // Billnumber itself is left in place on `sells` (still
                // useful for display/search, e.g. Excel export's Bill #
                // column) -- BillId is the new authoritative link Core.Data.
                // Sells.ReadSellsByBillId and everywhere that must not mix
                // one revision's lines with another's now uses instead.
                EnsureColumn(conn, "sells", "BillId", "INTEGER");

                // One-time backfill, same reasoning as the bills.IsCurrent
                // backfill above: every sells row that existed before this
                // feature was sold under a bill that -- at the time -- was
                // still the ONLY bills row with that Billnumber (revisioning
                // didn't exist yet), so matching purely on Billnumber here
                // is exact, not a guess, for every row this backfill will
                // ever actually touch. ORDER BY ID ASC LIMIT 1 is belt-and-
                // suspenders for that same reason -- there should only ever
                // be one match at backfill time, but if this somehow runs
                // again after revisions already exist, it picks the
                // earliest (original) rather than leaving it ambiguous.
                try
                {
                    using (var cmd = new SQLiteCommand(
                        @"UPDATE sells SET BillId = (
                              SELECT ID FROM bills
                              WHERE bills.Billnumber = sells.Billnumber
                              ORDER BY bills.ID ASC LIMIT 1
                          ) WHERE BillId IS NULL", conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
                catch (SQLiteException)
                {
                    // Not fatal -- see comment above. A row this misses
                    // just won't be found by BillId-based lookups until
                    // fixed by hand; Billnumber-based reads elsewhere are
                    // untouched either way.
                }

                // De-duplicates bills.ID (2026-08-29) -- discovered live via
                // "UNIQUE constraint failed: bills_rebuild.ID" the first
                // time the primary-key-move rebuild below actually ran.
                // Unlike Billnumber (genuinely unique the whole time -- it
                // was this table's real PRIMARY KEY all along, confirmed by
                // this method's own earlier diagnostic log), ID turns out to
                // have pre-existing duplicate values in this live
                // rovaShop.db, despite every INSERT this app's own code has
                // ever issued computing ID via a MAX(ID)+1 scan first --
                // however that happened historically (a manual edit, a
                // restored/merged backup, some older code path before this
                // rebuild), ID can't safely become the table's new PRIMARY
                // KEY as-is. Left unfixed, this wouldn't just block that
                // rebuild -- it would silently corrupt
                // BillsBrowserViewModel/Sells.ReadSellsByBillId's
                // per-revision line-item lookups for any two bills that
                // happen to collide, mixing one receipt's items into
                // another's.
                //
                // Billnumber is the reliable pivot for fixing this, since it
                // has always been genuinely unique: every sells row for a
                // given bill can still be found unambiguously by Billnumber
                // even while ID is broken. For every ID value shared by more
                // than one row, every row but the first (lowest Billnumber)
                // keeps its own Billnumber but is assigned a brand-new,
                // actually-unique ID (current table max + 1, incrementing
                // per fix) -- and every sells row for THAT Billnumber has
                // its BillId updated to match in the same transaction, so
                // the sells.BillId <-> bills.ID link this feature depends on
                // stays correct throughout, not just bills.ID itself.
                try
                {
                    int nextFreeId = 1;
                    using (var cmd = new SQLiteCommand("SELECT MAX(ID) FROM bills", conn))
                    {
                        var result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value) nextFreeId = Convert.ToInt32(result) + 1;
                    }

                    int nullIdsFixed = 0;
                    int duplicatesFixed = 0;

                    using (var tx = conn.BeginTransaction())
                    {
                        // Step 1: NULL IDs. Discovered live (2026-08-29) via
                        // an InvalidCastException the first time the
                        // duplicate-ID query below actually ran --
                        // "GROUP BY ID" in SQLite (like most SQL engines)
                        // treats every NULL as belonging to the SAME group,
                        // so any row with a NULL ID was being caught by the
                        // "HAVING COUNT(*) > 1" duplicate check below, then
                        // failing to Convert.ToInt32 a DBNull. A NULL ID
                        // isn't really a "duplicate" the way two rows
                        // sharing an actual number are -- there's no
                        // existing value to keep -- so every row with a
                        // NULL ID gets a fresh unique one here, not just
                        // "all but the first" the way an actual duplicate
                        // group is handled in step 2 below.
                        var nullIdBillnumbers = new System.Collections.Generic.List<int>();
                        using (var cmd = new SQLiteCommand(
                            "SELECT Billnumber FROM bills WHERE ID IS NULL", conn, tx))
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read()) nullIdBillnumbers.Add(Convert.ToInt32(reader["Billnumber"]));
                        }

                        foreach (int billnumberToFix in nullIdBillnumbers)
                        {
                            int newId = nextFreeId++;
                            using (var cmd = new SQLiteCommand(
                                "UPDATE sells SET BillId = @newid WHERE Billnumber = @billnumber", conn, tx))
                            {
                                cmd.Parameters.AddWithValue("@newid", newId);
                                cmd.Parameters.AddWithValue("@billnumber", billnumberToFix);
                                cmd.ExecuteNonQuery();
                            }
                            using (var cmd = new SQLiteCommand(
                                "UPDATE bills SET ID = @newid WHERE Billnumber = @billnumber", conn, tx))
                            {
                                cmd.Parameters.AddWithValue("@newid", newId);
                                cmd.Parameters.AddWithValue("@billnumber", billnumberToFix);
                                cmd.ExecuteNonQuery();
                            }
                            nullIdsFixed++;
                        }

                        // Step 2: actual duplicate (non-NULL) IDs -- see
                        // this whole block's opening comment. WHERE
                        // ID IS NOT NULL is now belt-and-suspenders rather
                        // than strictly required (step 1 just fixed every
                        // NULL), but costs nothing to keep explicit.
                        var duplicateIds = new System.Collections.Generic.List<int>();
                        using (var cmd = new SQLiteCommand(
                            "SELECT ID FROM bills WHERE ID IS NOT NULL GROUP BY ID HAVING COUNT(*) > 1", conn, tx))
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read()) duplicateIds.Add(Convert.ToInt32(reader["ID"]));
                        }

                        foreach (int dupId in duplicateIds)
                        {
                            var billnumbers = new System.Collections.Generic.List<int>();
                            using (var cmd = new SQLiteCommand(
                                "SELECT Billnumber FROM bills WHERE ID = @id ORDER BY Billnumber ASC", conn, tx))
                            {
                                cmd.Parameters.AddWithValue("@id", dupId);
                                using (var reader = cmd.ExecuteReader())
                                {
                                    while (reader.Read()) billnumbers.Add(Convert.ToInt32(reader["Billnumber"]));
                                }
                            }

                            // Keep the first (lowest Billnumber) at its
                            // current ID; every other bill sharing this ID
                            // gets reassigned a fresh one.
                            for (int i = 1; i < billnumbers.Count; i++)
                            {
                                int billnumberToFix = billnumbers[i];
                                int newId = nextFreeId++;

                                using (var cmd = new SQLiteCommand(
                                    "UPDATE sells SET BillId = @newid WHERE Billnumber = @billnumber", conn, tx))
                                {
                                    cmd.Parameters.AddWithValue("@newid", newId);
                                    cmd.Parameters.AddWithValue("@billnumber", billnumberToFix);
                                    cmd.ExecuteNonQuery();
                                }
                                using (var cmd = new SQLiteCommand(
                                    "UPDATE bills SET ID = @newid WHERE Billnumber = @billnumber", conn, tx))
                                {
                                    cmd.Parameters.AddWithValue("@newid", newId);
                                    cmd.Parameters.AddWithValue("@billnumber", billnumberToFix);
                                    cmd.ExecuteNonQuery();
                                }
                                duplicatesFixed++;
                            }
                        }

                        tx.Commit();
                    }

                    try
                    {
                        string logPath = System.IO.Path.Combine(Server.Location, "schema-migration-error.log");
                        System.IO.File.AppendAllText(logPath,
                            DateTime.Now + " -- bills.ID de-duplication ran. " +
                            $"nullIdsFixed={nullIdsFixed}, rowsReassigned={duplicatesFixed}\r\n\r\n");
                    }
                    catch (Exception)
                    {
                        // Nothing more to do if even this fails.
                    }
                }
                catch (Exception ex)
                {
                    // If this fails, the primary-key-move rebuild below will
                    // very likely fail again too (same root cause) -- logged
                    // for the same reason as every other diagnostic in this
                    // block: so that shows up as an explained failure rather
                    // than the same unexplained UNIQUE-constraint symptom a
                    // third time.
                    try
                    {
                        string logPath = System.IO.Path.Combine(Server.Location, "schema-migration-error.log");
                        System.IO.File.AppendAllText(logPath,
                            DateTime.Now + " -- bills.ID de-duplication failed:\r\n" +
                            ex + "\r\n\r\n");
                    }
                    catch (Exception)
                    {
                        // Nothing more to do if even this fails.
                    }
                }

                // Removes bills.Billnumber's uniqueness constraint
                // (2026-08-29) -- confirmed live via a "UNIQUE constraint
                // failed: bills.Billnumber" error the first time a return
                // was attempted after shipping receipt revisioning above,
                // and confirmed exactly WHY via this method's own diagnostic
                // log (added the same day): Billnumber -- not ID -- turned
                // out to be this table's actual PRIMARY KEY AUTOINCREMENT
                // column (SQLite's rowid alias, unconditionally unique by
                // definition, with no separate "UNIQUE" keyword anywhere in
                // its declaration for a first pass of this fix to find by
                // searching the table's SQL text for that literal word --
                // that first attempt is why this comment now says "first
                // time" above rather than being the whole story). Nothing in
                // this codebase or in DatabaseBootstrapper ever declared
                // that -- it must already be baked into this live
                // rovaShop.db from before this rebuild.
                //
                // ID, not Billnumber, is what this app's OWN code has always
                // actually treated as a bill row's unique identity --
                // CheckoutViewModel.CompleteSale and BillsBrowserViewModel.
                // NextBillId both compute a "next ID" by scanning MAX(ID)
                // and have always kept every row's ID distinct, entirely
                // independent of whatever SQLite itself was separately
                // enforcing on Billnumber underneath. So the fix isn't just
                // "remove the constraint" -- it's "move the primary key from
                // Billnumber onto ID", the column this app was already
                // relying on for that role.
                //
                // SQLite has no ALTER TABLE ... DROP CONSTRAINT and no way to
                // relax an INTEGER PRIMARY KEY column back to an ordinary
                // one in place, so this rebuilds the table via SQLite's own
                // documented procedure for exactly this situation: create a
                // new table from the current columns (read from PRAGMA
                // table_info, so every column this table actually has --
                // including CustomerId/IsCurrent/RevisionSuffix added above
                // this same run -- is preserved, along with each column's
                // real NOT NULL/DEFAULT), but with the PRIMARY KEY
                // AUTOINCREMENT moved onto ID instead of Billnumber; copy
                // every row across positionally (INSERT INTO ... SELECT * --
                // correct regardless of column NAMES since both tables are
                // built in the same PRAGMA table_info order, and safe
                // specifically because this app's own ID values have always
                // been unique, per the paragraph above -- there's nothing to
                // deduplicate); drop the old table; rename the new one into
                // its place.
                try
                {
                    var columns = new System.Collections.Generic.List<(string Name, string Type, bool NotNull, string Default, bool IsPk)>();
                    using (var cmd = new SQLiteCommand("PRAGMA table_info(bills)", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            columns.Add((
                                reader["name"].ToString(),
                                reader["type"].ToString(),
                                Convert.ToInt32(reader["notnull"]) == 1,
                                reader["dflt_value"] == DBNull.Value ? null : reader["dflt_value"].ToString(),
                                Convert.ToInt32(reader["pk"]) == 1));
                        }
                    }

                    bool billnumberIsPk = columns.Exists(c =>
                        string.Equals(c.Name, "Billnumber", StringComparison.OrdinalIgnoreCase) && c.IsPk);

                    int indexesDropped = 0;
                    using (var cmd = new SQLiteCommand(
                        "SELECT name FROM sqlite_master WHERE type='index' AND tbl_name='bills' AND sql LIKE '%UNIQUE%'", conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string indexName = reader["name"].ToString();
                            using (var dropCmd = new SQLiteCommand($"DROP INDEX IF EXISTS {indexName}", conn))
                            {
                                dropCmd.ExecuteNonQuery();
                            }
                            indexesDropped++;
                        }
                    }

                    string tableSql = null;
                    using (var cmd = new SQLiteCommand(
                        "SELECT sql FROM sqlite_master WHERE type='table' AND name='bills'", conn))
                    {
                        var result = cmd.ExecuteScalar();
                        tableSql = result?.ToString();
                    }
                    bool hasInlineUnique = tableSql != null && tableSql.IndexOf("UNIQUE", StringComparison.OrdinalIgnoreCase) >= 0;

                    bool rebuildNeeded = billnumberIsPk || hasInlineUnique;

                    if (rebuildNeeded)
                    {
                        var columnDefs = new System.Collections.Generic.List<string>();
                        foreach (var col in columns)
                        {
                            // The actual fix: ID becomes the real PRIMARY
                            // KEY AUTOINCREMENT column, Billnumber becomes an
                            // ordinary (repeatable) INTEGER column -- see
                            // this whole block's opening comment for why.
                            bool makePrimaryKey = string.Equals(col.Name, "ID", StringComparison.OrdinalIgnoreCase);

                            string def = $"\"{col.Name}\" {col.Type}";
                            if (makePrimaryKey)
                            {
                                def += " PRIMARY KEY AUTOINCREMENT";
                            }
                            else
                            {
                                if (col.NotNull) def += " NOT NULL";
                                if (col.Default != null) def += $" DEFAULT {col.Default}";
                            }
                            columnDefs.Add(def);
                        }

                        using (var tx = conn.BeginTransaction())
                        {
                            using (var cmd = new SQLiteCommand(
                                $"CREATE TABLE bills_rebuild ({string.Join(", ", columnDefs)})", conn, tx))
                            {
                                cmd.ExecuteNonQuery();
                            }
                            using (var cmd = new SQLiteCommand(
                                "INSERT INTO bills_rebuild SELECT * FROM bills", conn, tx))
                            {
                                cmd.ExecuteNonQuery();
                            }
                            using (var cmd = new SQLiteCommand("DROP TABLE bills", conn, tx))
                            {
                                cmd.ExecuteNonQuery();
                            }
                            using (var cmd = new SQLiteCommand(
                                "ALTER TABLE bills_rebuild RENAME TO bills", conn, tx))
                            {
                                cmd.ExecuteNonQuery();
                            }
                            tx.Commit();
                        }
                    }

                    // Always logged (2026-08-29), success path included --
                    // not just the failure path below -- since "ran, found
                    // nothing to fix" (detection logic wrong, as it turned
                    // out to be on the first pass) and "never ran at all"
                    // (old build still running) look identical from outside
                    // without this: both leave the exact same symptom.
                    try
                    {
                        string logPath = System.IO.Path.Combine(Server.Location, "schema-migration-error.log");
                        System.IO.File.AppendAllText(logPath,
                            DateTime.Now + " -- bills primary-key check ran. " +
                            $"billnumberIsPk={billnumberIsPk}, hasInlineUnique={hasInlineUnique}, " +
                            $"indexesDropped={indexesDropped}, rebuildNeeded={rebuildNeeded}\r\n\r\n");
                    }
                    catch (Exception)
                    {
                        // Nothing more to do if even this fails.
                    }
                }
                catch (Exception ex)
                {
                    // Widened from SQLiteException to Exception, and
                    // logged (2026-08-29) -- unlike every other try/catch in
                    // this file, a failure HERE means returns keep failing
                    // with the same UNIQUE constraint error forever, not
                    // just a nice-to-have backfill silently not applying,
                    // and a bare silent catch already once gave zero way to
                    // tell WHY it failed when this exact symptom was
                    // reported after this exact code had already run.
                    // Logs next to the database itself (Documents\PosSystem,
                    // same folder Mahmoud already knows about from Settings'
                    // Data & Backup section) rather than anywhere requiring
                    // its own separate UI to surface -- still wrapped in its
                    // own try/catch since a failure while trying to WRITE a
                    // diagnostic log must never be what crashes the app.
                    try
                    {
                        string logPath = System.IO.Path.Combine(Server.Location, "schema-migration-error.log");
                        System.IO.File.AppendAllText(logPath,
                            DateTime.Now + " -- bills primary-key fix failed:\r\n" +
                            ex + "\r\n\r\n");
                    }
                    catch (Exception)
                    {
                        // Nothing more to do if even this fails -- see
                        // comment above.
                    }
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
