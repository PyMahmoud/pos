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
        public string connectionString = String.Format("Data Source = {0}", fullpath);

        static Server()
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

