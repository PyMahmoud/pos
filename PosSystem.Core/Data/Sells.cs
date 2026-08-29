using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PosSystem.Core.Data
{
    public class Sells
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
        // BillId added 2026-08-28 for receipt revisioning (see
        // DatabaseBootstrapper's matching comment) -- required, not
        // optional: every caller from now on knows exactly which bills.ID
        // row a line belongs to (CheckoutViewModel.CompleteSale has just
        // inserted that row itself; BillsBrowserViewModel's return flow is
        // inserting the replacement row right alongside these lines), so
        // there's no legitimate "don't know it" case the way CustomerId on
        // InsertBills has for a walk-in sale.
        public void InsertSells(string TableName, string Name, string Category, double Quantity, double Cost, double Price, string Type, string Time, string Datex, string Barcode ,int Billnumber, double Earned ,string Returned , string Details, int BillId)
        {
            string insertString = "insert into " + TableName + "(Name ,Category ,Quantity ,Cost ,Price ,Type  , Time ,  Datex ,Barcode ,Billnumber , Earned ,Returned , Details, BillId) VALUES (@name , @category , @quantity , @cost , @price ,@type ,@time , @datex ,@barcode , @billnumber ,@earned ,@returned , @details, @billid)";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(insertString, conn))
                {
                    cmd.Parameters.AddWithValue("@name", Name);
                    cmd.Parameters.AddWithValue("@category", Category);
                    cmd.Parameters.AddWithValue("@quantity", Quantity);
                    cmd.Parameters.AddWithValue("@cost", Cost);
                    cmd.Parameters.AddWithValue("@price", Price);
                    cmd.Parameters.AddWithValue("@type", Type);
                    cmd.Parameters.AddWithValue("@time", Time);
                    cmd.Parameters.AddWithValue("@datex", Datex);
                    cmd.Parameters.AddWithValue("@barcode", Barcode);
                    cmd.Parameters.AddWithValue("@billnumber", Billnumber);
                    cmd.Parameters.AddWithValue("@earned", Earned);
                    cmd.Parameters.AddWithValue("@returned", Returned);
                    cmd.Parameters.AddWithValue("@details", Details);
                    cmd.Parameters.AddWithValue("@billid", BillId);
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
        public List<Models.Sells> ReadPendingSell(string TableName)
        {

            List<Models.Sells> goods = new List<Models.Sells>();
            string readString = "SELECT * FROM " + TableName + " ";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(readString, conn))
                {
                    IDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var goods_List = new Models.Sells();
                        goods_List.Id = DbNullSafe.ToInt32(reader["ID"]);
                        goods_List.Name = DbNullSafe.ToStringSafe(reader["Name"]);
                        goods_List.Category = DbNullSafe.ToStringSafe(reader["Category"]);
                        goods_List.Quantity = DbNullSafe.ToDouble(reader["Quantity"]);
                        goods_List.Cost = DbNullSafe.ToDouble(reader["Cost"]);
                        goods_List.Price = DbNullSafe.ToDouble(reader["Price"]);
                        goods_List.Type = DbNullSafe.ToStringSafe(reader["Type"]);
                        goods_List.Time = DbNullSafe.ToStringSafe(reader["Time"]);
                        goods_List.Datex = DbNullSafe.ToStringSafe(reader["Datex"]);
                        goods_List.Barcode = DbNullSafe.ToStringSafe(reader["Barcode"]);
                        goods_List.Billnumber = DbNullSafe.ToInt32(reader["Billnumber"]);
                        goods_List.Earned = DbNullSafe.ToDouble(reader["Earned"]);
                        goods_List.Returned = DbNullSafe.ToStringSafe(reader["Returned"]);
                        goods_List.Details = DbNullSafe.ToStringSafe(reader["Details"]);
                        goods_List.BillId = DbNullSafe.ToInt32(reader["BillId"]);
                        goods.Add(goods_List);
                      
                    }
                    return goods;
                }
            }
        }
        public List<Models.Goods> ReadGoodsPic(string TableName , string Category)
        {

            List<Models.Goods> goods = new List<Models.Goods>();
            string readString = "SELECT * FROM " + TableName + " Where Category=@category";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(readString, conn))
                {
                    cmd.Parameters.AddWithValue("@category", Category);
                    IDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var goods_List = new Models.Goods();
                        goods_List.Id = Convert.ToInt32(reader["ID"]);
                        goods_List.Name = reader["Name"].ToString();
                       
                        goods.Add(goods_List);

                    }
                    return goods;
                }
            }
        }

        // Everything below added 2026-08-27 for item #6 (Bills view on
        // Checkout + delete-line/delete-whole-bill with reversal).

        // A bill's line items, for the drill-down view when someone taps a
        // bill in the list -- and the input to "restore inventory for every
        // line" when the whole bill is deleted (see BillsViewModel.
        // DeleteWholeBill).
        public List<Models.Sells> ReadSellsByBillnumber(string TableName, int Billnumber)
        {
            List<Models.Sells> sells = new List<Models.Sells>();
            string readString = "SELECT * FROM " + TableName + " WHERE Billnumber = @billnumber";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(readString, conn))
                {
                    cmd.Parameters.AddWithValue("@billnumber", Billnumber);
                    IDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var goods_List = new Models.Sells();
                        goods_List.Id = DbNullSafe.ToInt32(reader["ID"]);
                        goods_List.Name = DbNullSafe.ToStringSafe(reader["Name"]);
                        goods_List.Category = DbNullSafe.ToStringSafe(reader["Category"]);
                        goods_List.Quantity = DbNullSafe.ToDouble(reader["Quantity"]);
                        goods_List.Cost = DbNullSafe.ToDouble(reader["Cost"]);
                        goods_List.Price = DbNullSafe.ToDouble(reader["Price"]);
                        goods_List.Type = DbNullSafe.ToStringSafe(reader["Type"]);
                        goods_List.Time = DbNullSafe.ToStringSafe(reader["Time"]);
                        goods_List.Datex = DbNullSafe.ToStringSafe(reader["Datex"]);
                        goods_List.Barcode = DbNullSafe.ToStringSafe(reader["Barcode"]);
                        goods_List.Billnumber = DbNullSafe.ToInt32(reader["Billnumber"]);
                        goods_List.Earned = DbNullSafe.ToDouble(reader["Earned"]);
                        goods_List.Returned = DbNullSafe.ToStringSafe(reader["Returned"]);
                        goods_List.Details = DbNullSafe.ToStringSafe(reader["Details"]);
                        goods_List.BillId = DbNullSafe.ToInt32(reader["BillId"]);
                        sells.Add(goods_List);
                    }
                    return sells;
                }
            }
        }

        // Added 2026-08-28 for receipt revisioning (see
        // DatabaseBootstrapper's matching comment) -- the per-revision
        // counterpart to ReadSellsByBillnumber above. Once a receipt has
        // been returned-from, several bills rows can share one Billnumber
        // (the original plus every revision), so ReadSellsByBillnumber
        // would pull in EVERY revision's lines mixed together; this reads
        // only the lines belonging to one specific bills.ID -- what
        // BillsBrowserViewModel now uses whenever it needs "exactly this
        // receipt version's items", whether that's the current revision
        // (for the return UI) or a superseded one (for read-only history).
        public List<Models.Sells> ReadSellsByBillId(string TableName, int BillId)
        {
            List<Models.Sells> sells = new List<Models.Sells>();
            string readString = "SELECT * FROM " + TableName + " WHERE BillId = @billid";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(readString, conn))
                {
                    cmd.Parameters.AddWithValue("@billid", BillId);
                    IDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var goods_List = new Models.Sells();
                        goods_List.Id = DbNullSafe.ToInt32(reader["ID"]);
                        goods_List.Name = DbNullSafe.ToStringSafe(reader["Name"]);
                        goods_List.Category = DbNullSafe.ToStringSafe(reader["Category"]);
                        goods_List.Quantity = DbNullSafe.ToDouble(reader["Quantity"]);
                        goods_List.Cost = DbNullSafe.ToDouble(reader["Cost"]);
                        goods_List.Price = DbNullSafe.ToDouble(reader["Price"]);
                        goods_List.Type = DbNullSafe.ToStringSafe(reader["Type"]);
                        goods_List.Time = DbNullSafe.ToStringSafe(reader["Time"]);
                        goods_List.Datex = DbNullSafe.ToStringSafe(reader["Datex"]);
                        goods_List.Barcode = DbNullSafe.ToStringSafe(reader["Barcode"]);
                        goods_List.Billnumber = DbNullSafe.ToInt32(reader["Billnumber"]);
                        goods_List.Earned = DbNullSafe.ToDouble(reader["Earned"]);
                        goods_List.Returned = DbNullSafe.ToStringSafe(reader["Returned"]);
                        goods_List.Details = DbNullSafe.ToStringSafe(reader["Details"]);
                        goods_List.BillId = DbNullSafe.ToInt32(reader["BillId"]);
                        sells.Add(goods_List);
                    }
                    return sells;
                }
            }
        }

        public bool DeleteSellById(string TableName, int Id)
        {
            string deleteString = "DELETE FROM " + TableName + " WHERE ID = @id";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(deleteString, conn))
                {
                    cmd.Parameters.AddWithValue("@id", Id);
                    cmd.ExecuteNonQuery();
                    cmd.Dispose();
                    return true;
                }
            }
        }

        public bool DeleteSellsByBillnumber(string TableName, int Billnumber)
        {
            string deleteString = "DELETE FROM " + TableName + " WHERE Billnumber = @billnumber";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(deleteString, conn))
                {
                    cmd.Parameters.AddWithValue("@billnumber", Billnumber);
                    cmd.ExecuteNonQuery();
                    cmd.Dispose();
                    return true;
                }
            }
        }

        // Added 2026-08-28 for the Bills browser's per-line +/- quantity
        // adjustment (Mahmoud asked for a way to remove/add back a specific
        // amount of a line instead of only being able to delete it
        // outright). Quantity and Earned are the only two fields that
        // actually shift when a line's quantity changes -- Name/Category/
        // Cost/Price/Barcode/Time/Datex/Billnumber describe what was sold
        // and at what per-unit terms, none of which change just because the
        // quantity sold on this one line did. Earned is passed in rather
        // than recomputed here so the caller (BillsBrowserViewModel) stays
        // the single place that knows the (Price - Cost) * Quantity formula
        // -- same division of responsibility DeleteLine's own bill-recompute
        // math already follows.
        public bool UpdateSellQuantity(string TableName, int Id, double Quantity, double Earned)
        {
            string updateString = "UPDATE " + TableName + " SET Quantity = @quantity, Earned = @earned WHERE ID = @id";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(updateString, conn))
                {
                    cmd.Parameters.AddWithValue("@quantity", Quantity);
                    cmd.Parameters.AddWithValue("@earned", Earned);
                    cmd.Parameters.AddWithValue("@id", Id);
                    cmd.ExecuteNonQuery();
                    cmd.Dispose();
                    return true;
                }
            }
        }
    }
}
