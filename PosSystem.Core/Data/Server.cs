using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace PosSystem.Core.Data
{
    public class Server
    {
        // Changed 2026-08-27, round 2: moved again, this time from
        // %AppData%\PosSystem to Documents\PosSystem, per Mahmoud's
        // explicit request (item #2 of the 2026-08-27 batch) — AppData is
        // hidden by default in Explorer, making it hard for a
        // non-technical shop owner to find/back up the file himself
        // outside the in-app Backup Now button on Settings. Documents is
        // visible, expected, and still per-user writable, still untouched
        // by an installer overwriting Program Files, still survives an
        // app update/uninstall — same survival guarantee as AppData, just
        // in a folder a person would actually look in.
        //
        // Originally the database lived next to the .exe
        // (AppDomain.CurrentDomain.BaseDirectory — i.e. bin\Debug or
        // bin\Release), which an app update/uninstall would take down with
        // it — that's why this moved at all, first to AppData
        // (2026-08-27, round 1) and now to Documents.
        //
        // Two-step migration, both one-time and non-destructive: if
        // nothing exists yet at the new Documents path, first look at the
        // AppData path (any client machine that already ran the round-1
        // build), then the original next-to-exe path (any machine that
        // never updated past the very first build) — copy the first one
        // found, don't move, so nothing is ever deleted from an old
        // location even if the copy silently fails partway. Runs in the
        // static constructor so it happens exactly once per process,
        // before anything (Server.fullpath / connectionString) is read —
        // static field/cctor ordering in C# guarantees the Old*Location
        // fields are assigned before the constructor body below runs.
        private static readonly string OldExeLocation = System.AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string OldAppDataLocation = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PosSystem");
        public static string Location = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "PosSystem");
        public static string fileName = "rovaShop.db";
        public static string fullpath = Path.Combine(Location, fileName);
        // BusyTimeout added 2026-08-28 (bug report: deleting a bill line
        // freezes the whole app, then shows "database is locked"). Root
        // cause: a single delete-line action opens ~15-20 short-lived
        // connections back to back (read lines, look up the product,
        // update its quantity, delete the sold line, re-total the bill,
        // adjust the customer balance, then Dashboard/Inventory's
        // event-driven refreshes each open several more) -- every one of
        // them correctly closed via `using`, but with no BusyTimeout set,
        // System.Data.SQLite's default is 0: the instant any single
        // connection finds the file still settling from the one before it
        // (a real risk on Windows -- antivirus scanning a just-closed
        // handle, or the file being under OneDrive sync now that it lives
        // in Documents, see Server's own class comment on that move), it
        // doesn't wait at all -- it fails immediately. What actually
        // caused the FREEZE specifically (not just an instant error) is
        // System.Data.SQLite's own internal retry loop for a SQLITE_BUSY
        // hit, which runs on the calling thread (the UI thread here, since
        // every Data/*.cs call in this app is synchronous) for its default
        // command timeout before finally giving up and throwing -- that's
        // the multi-second freeze immediately before the error message
        // appeared in the report.
        //
        // 5000ms here means: if a connection ever does find the file
        // briefly locked by something else, it waits up to 5 seconds
        // (SQLite polls/retries internally during that window, not one
        // long single wait) before giving up -- long enough to ride out a
        // transient AV/sync hiccup, short enough that a genuine problem
        // still surfaces in a few seconds rather than the driver's much
        // longer default. Paired with `PRAGMA journal_mode=WAL` in
        // DatabaseBootstrapper.EnsureSchema() (run once at startup) -- WAL
        // lets readers and a writer proceed concurrently without blocking
        // each other in the first place, which should eliminate the
        // vast majority of these lock conflicts outright; BusyTimeout is
        // the safety net for whatever WAL doesn't cover (e.g. two writers
        // landing at literally the same instant).
        public string connectionString = String.Format("Data Source = {0};BusyTimeout=5000", fullpath);

        static Server()
        {
            // Wrapped 2026-08-30: this whole block used to run unguarded.
            // Any I/O failure here (locked file, permissions, a weird path
            // resolution under Wine, AV holding a handle, etc.) would throw
            // inside a static constructor, which C# always rewraps as an
            // opaque TypeInitializationException — the real cause gets
            // hidden and the app hard-crashes before a single window shows,
            // with no chance for App.xaml.cs's handlers to log anything
            // useful. Migration is a nice-to-have, not something that
            // should be able to take the whole app down: best effort, log
            // and continue on failure. If Location itself can't be created,
            // that's more serious (nothing can be read/written at all) —
            // still don't crash the static ctor, but that failure will
            // resurface immediately and loudly the moment any connection
            // string is actually opened.
            try
            {
                Directory.CreateDirectory(Location);

                if (!File.Exists(fullpath))
                {
                    string oldAppDataFullPath = Path.Combine(OldAppDataLocation, fileName);
                    string oldExeFullPath = Path.Combine(OldExeLocation, fileName);

                    if (File.Exists(oldAppDataFullPath))
                    {
                        // Copy, don't move — leaving the old copy in place is
                        // deliberately harmless and far safer than deleting a
                        // client's only copy of their data if the copy
                        // silently fails partway for some reason.
                        File.Copy(oldAppDataFullPath, fullpath);
                    }
                    else if (File.Exists(oldExeFullPath))
                    {
                        File.Copy(oldExeFullPath, fullpath);
                    }
                }
            }
            catch (Exception ex)
            {
                try
                {
                    string logPath = Path.Combine(Path.GetTempPath(), "RovaShop_migration_error.log");
                    File.AppendAllText(logPath, DateTime.Now + ": " + ex + Environment.NewLine);
                }
                catch
                {
                    // Logging itself failing is not something to crash over either.
                }
            }
        }

        public void CreateDatabase(string createTableCommand)
        {
            string createTable = createTableCommand;
            if(!DuplicateDatabase(fullpath))
            {
                using (SQLiteConnection conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();
                    using (SQLiteCommand cmd = new SQLiteCommand(createTable, conn))
                    {
                        cmd.ExecuteNonQuery();
                        cmd.Dispose();
                    }
                }
            }
        }
        public void CreateTable(string TableName)
        {
            string createTable = TableName;
            using (SQLiteConnection conn = new SQLiteConnection(connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(createTable, conn))
                {
                    cmd.ExecuteNonQuery();
                    cmd.Dispose();
                }
            }
        }
        public bool DuplicateDatabase(string fullPath)
        {
            return File.Exists(fullPath);
        }

    }
}

