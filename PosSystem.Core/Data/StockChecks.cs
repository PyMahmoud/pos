using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;

namespace PosSystem.Core.Data
{
    /// <summary>
    /// CRUD for the `stockchecks` table (see DatabaseBootstrapper for the
    /// schema and why it exists). No Update/Delete — every visit is a new
    /// row by design, per the client's requirement to see trends over time
    /// rather than just the latest reading.
    /// </summary>
    public class StockChecks
    {
        Server server = new Server();

        public void InsertStockCheck(int CustomerId, string GoodBarcode, string MedicationName,
            double Quantity, string BatchNumber, string ExpiryDate, string CheckDate, string CheckTime, string Notes)
        {
            string insertString = "insert into stockchecks " +
                "(CustomerId, GoodBarcode, MedicationName, Quantity, BatchNumber, ExpiryDate, CheckDate, CheckTime, Notes) " +
                "VALUES (@customerid, @barcode, @name, @quantity, @batch, @expiry, @checkdate, @checktime, @notes)";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(insertString, conn))
                {
                    cmd.Parameters.AddWithValue("@customerid", CustomerId);
                    cmd.Parameters.AddWithValue("@barcode", GoodBarcode ?? "");
                    cmd.Parameters.AddWithValue("@name", MedicationName ?? "");
                    cmd.Parameters.AddWithValue("@quantity", Quantity);
                    cmd.Parameters.AddWithValue("@batch", BatchNumber ?? "");
                    cmd.Parameters.AddWithValue("@expiry", ExpiryDate ?? "");
                    cmd.Parameters.AddWithValue("@checkdate", CheckDate ?? "");
                    cmd.Parameters.AddWithValue("@checktime", CheckTime ?? "");
                    cmd.Parameters.AddWithValue("@notes", Notes ?? "");
                    cmd.ExecuteNonQuery();
                    cmd.Dispose();
                }
            }
        }

        // Ordered by ID (not CheckDate) descending — CheckDate is stored as
        // "dd/MM/yyyy" text to match every other date field in this schema,
        // which does NOT sort correctly as a string (e.g. "05/12/2026"
        // sorts before "20/01/2026" lexically). ID is autoincrement, so
        // "newest first" and "highest ID first" are equivalent without
        // needing to parse dates back out just to sort them.
        public List<Models.StockCheck> ReadByCustomer(int CustomerId)
        {
            var results = new List<Models.StockCheck>();
            string readString = "SELECT * FROM stockchecks WHERE CustomerId = @customerid ORDER BY ID DESC";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(readString, conn))
                {
                    cmd.Parameters.AddWithValue("@customerid", CustomerId);
                    IDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        results.Add(new Models.StockCheck
                        {
                            Id = DbNullSafe.ToInt32(reader["ID"]),
                            CustomerId = DbNullSafe.ToInt32(reader["CustomerId"]),
                            GoodBarcode = DbNullSafe.ToStringSafe(reader["GoodBarcode"]),
                            MedicationName = DbNullSafe.ToStringSafe(reader["MedicationName"]),
                            Quantity = DbNullSafe.ToDouble(reader["Quantity"]),
                            BatchNumber = DbNullSafe.ToStringSafe(reader["BatchNumber"]),
                            ExpiryDate = DbNullSafe.ToStringSafe(reader["ExpiryDate"]),
                            CheckDate = DbNullSafe.ToStringSafe(reader["CheckDate"]),
                            CheckTime = DbNullSafe.ToStringSafe(reader["CheckTime"]),
                            Notes = DbNullSafe.ToStringSafe(reader["Notes"])
                        });
                    }
                    return results;
                }
            }
        }
    }
}
