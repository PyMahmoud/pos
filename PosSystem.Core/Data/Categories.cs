using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PosSystem.Core.Data
{
    public class Categories
    {
        Server server = new Server();

        //        CREATE TABLE `goods` (
        //	`ID`	INTEGER PRIMARY KEY AUTOINCREMENT,
        //	`Name`	TEXT,
        //	`Category`	TEXT,
        //	`Quantity`	REAL,
        //	`Cost`	REAL,
        //	`Price`	REAL,
        //	`Type`	TEXT,
        //	`Barcode`	TEXT,
        //	`Earned`	REAL,
        //	`Datex`	TEXT,
        //	`Datee`	TEXT,
        //	`Image`	BLOB
        //);
        public void InserCategories(string TableName, string Name, string Type ,byte[] Image)
        {
            string insertString = "insert into " + TableName + "(Name  ,Type , Image ) VALUES (@name  ,@type , @image)";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(insertString, conn))
                {
                    cmd.Parameters.AddWithValue("@name", Name);
                    cmd.Parameters.AddWithValue("@type", Type);
                    cmd.Parameters.AddWithValue("@image", Image);
                    cmd.ExecuteNonQuery();
                    cmd.Dispose();
                }
            }
        }
        public string ReadString(string TableName , string FieldValue , double ID)
        {
            string value;
            string readString = "SELECT " + FieldValue+ " FROM "+ TableName +" WHERE ID ="+ ID +" ";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(readString, conn))
                {
                    SQLiteDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        value = reader[FieldValue].ToString();  
                        cmd.Dispose();
                        return value;
                    }
                    
                  
                }
            }
            return "";
            
        }
        public DataTable ReadAdapter(string TableName)
        {
            
            DataTable DT = new DataTable();
            string readString = "SELECT * FROM " + TableName + " ";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteDataAdapter sQLiteDataAdapter = new SQLiteDataAdapter(readString, conn))
                {
                    sQLiteDataAdapter.Fill(DT);
                    return DT;
                }
                   
               
            }
        }
        public List<Models.Categories> ReadCategoriesPic(string TableName)
        {

            List<Models.Categories> cata = new List<Models.Categories>();
            string readString = "SELECT * FROM " + TableName + " ORDER BY Name ASC";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(readString, conn))
                {
                    IDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var cata_List = new Models.Categories();
                        cata_List.Id = Convert.ToInt32(reader["ID"]);
                        cata_List.Name = reader["Name"].ToString();
                        //cata_List.Image = (byte[])(reader["Image"]); //TODO
                        cata.Add(cata_List);

                    }
                    return cata;
                }
            }
        }

        // Everything below was added for Inventory's category management
        // feature (2026-08-25) — see DatabaseBootstrapper.EnsureSchema for
        // the `categories` table itself and why these are separate from
        // the legacy methods above rather than a replacement for them.
        // Deliberately name-only (no Type/Image parameters): this app has
        // no use for those legacy columns, and every method here works
        // whether the underlying table is this file's intended minimal
        // schema or a pre-existing richer one from the old app — none of
        // these reference Type or Image at all.

        // Updated 2026-08-25: was a plain "SELECT Name FROM categories",
        // which surfaced literal duplicate rows in every category picker
        // across the app (Add/Edit Product's category ComboBox, the
        // Delete-category picker below) -- the live database has
        // duplicate/case-variant rows in this table from before this
        // feature enforced uniqueness (see DatabaseBootstrapper.EnsureSchema
        // for the one-time cleanup pass that now also runs against the
        // table itself). GROUP BY ... COLLATE NOCASE folds case-variant
        // duplicates ('Snacks' / 'snacks') into one row here too, same as
        // CategoryExists' comparison already does; MIN(Name) picks a single
        // consistent spelling to display for each group.
        public List<string> ReadAllCategoryNames()
        {
            var names = new List<string>();
            string readString = "SELECT MIN(Name) AS Name FROM categories GROUP BY Name COLLATE NOCASE ORDER BY Name ASC";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(readString, conn))
                {
                    IDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        names.Add(reader["Name"].ToString());
                    }
                    return names;
                }
            }
        }

        // Case-insensitive on purpose — matches the COLLATE NOCASE unique
        // constraint DatabaseBootstrapper defines (when it's the one that
        // actually created the table) and every other category comparison
        // already in this app.
        public bool CategoryExists(string name)
        {
            string readString = "SELECT COUNT(*) FROM categories WHERE Name = @name COLLATE NOCASE";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(readString, conn))
                {
                    cmd.Parameters.AddWithValue("@name", name);
                    long count = (long)cmd.ExecuteScalar();
                    return count > 0;
                }
            }
        }

        // Caller (InventoryViewModel.AddCategory) is expected to call
        // CategoryExists first for a clean "already exists" message — this
        // just does the insert. Not wrapped in try/catch here the way
        // DatabaseBootstrapper's backfill is: a failure on an explicit,
        // user-initiated "add this category" click should surface as an
        // error to that user, not be silently swallowed.
        public void InsertCategoryName(string name)
        {
            string insertString = "INSERT INTO categories (Name) VALUES (@name)";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(insertString, conn))
                {
                    cmd.Parameters.AddWithValue("@name", name);
                    cmd.ExecuteNonQuery();
                    cmd.Dispose();
                }
            }
        }

        // Caller (InventoryViewModel.DeleteCategory) is expected to check
        // Goods.CountByCategory first and refuse the delete with a clear
        // message if any product still uses this category — deleting it
        // out from under existing products would leave their Category
        // field pointing at a name that no longer appears anywhere in the
        // picker, with no UI path back to it. This method itself has no
        // such guard, so it must not be called directly from XAML/without
        // that check.
        public bool DeleteCategoryByName(string name)
        {
            string deleteString = "DELETE FROM categories WHERE Name = @name COLLATE NOCASE";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(deleteString, conn))
                {
                    cmd.Parameters.AddWithValue("@name", name);
                    cmd.ExecuteNonQuery();
                    cmd.Dispose();
                    return true;
                }
            }
        }
    }
}
