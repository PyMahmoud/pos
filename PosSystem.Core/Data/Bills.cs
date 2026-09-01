using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PosSystem.Core.Data
{
    public class Bills
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
        //string billnumber, double billcost, string time, string datex, string ownername, string ownerid, string ownernumber, double paid, double remain, double earned, double tax, double discount
        public void InsertBills(string TableName,int ID, int Billnumber, double Billcost, string Time, string Datex, string Ownername, string Ownerid, string Ownernumber, double Paid, double Remain, double Earned, double Tax, double Discount, string Details, int? CustomerId = null, bool IsCurrent = true, string RevisionSuffix = null, double DiscountPercent = 0)
        {
            string insertString = "insert into " + TableName + "(ID ,Billnumber ,Billcost ,Time ,Datex ,Ownername ,Ownerid  , Ownernumber ,  Paid ,Remain ,Earned , Tax ,Discount , Details, CustomerId, IsCurrent, RevisionSuffix, DiscountPercent) VALUES (@id ,@billnumber , @billcost , @time , @datex , @ownername ,@ownerid ,@pwnernumber , @paid ,@remain , @earned ,@tax ,@discount , @details, @customerid, @iscurrent, @revisionsuffix, @discountpercent)";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(insertString, conn))
                {
                    cmd.Parameters.AddWithValue("@id", ID);
                    cmd.Parameters.AddWithValue("@billnumber", Billnumber);
                    cmd.Parameters.AddWithValue("@billcost", Billcost);
                    cmd.Parameters.AddWithValue("@time", Time);
                    cmd.Parameters.AddWithValue("@datex", Datex);
                    cmd.Parameters.AddWithValue("@ownername", Ownername);
                    cmd.Parameters.AddWithValue("@ownerid", Ownerid);
                    cmd.Parameters.AddWithValue("@pwnernumber", Ownernumber);
                    cmd.Parameters.AddWithValue("@paid", Paid);
                    cmd.Parameters.AddWithValue("@remain", Remain);
                    cmd.Parameters.AddWithValue("@earned", Earned);
                    cmd.Parameters.AddWithValue("@tax", Tax);
                    cmd.Parameters.AddWithValue("@discount", Discount);
                    cmd.Parameters.AddWithValue("@details", Details);
                    cmd.Parameters.AddWithValue("@customerid", (object)CustomerId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@iscurrent", IsCurrent ? 1 : 0);
                    cmd.Parameters.AddWithValue("@revisionsuffix", (object)RevisionSuffix ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@discountpercent", DiscountPercent);
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
        public string ReadBillnumber(string TableName , string Fieldname)
        {
            string returned_value = "";
            string readString = "SELECT * FROM " + TableName + " ORDER BY " + Fieldname + " DESC LIMIT 1";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(readString, conn))
                {
                    IDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        returned_value = reader["billnumber"].ToString();
                       
                    }
                    return returned_value;
                }
            }
        }
        public List<Models.Bills> ReadBills(string TableName)
        {

            List<Models.Bills> bills = new List<Models.Bills>();
            string readString = "SELECT * FROM " + TableName + " ";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(readString, conn))
                {
                    IDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        //string billnumber, double billcost, string time, string datex, string ownername, string ownerid, string ownernumber, double paid, double remain, double earned, double tax, double discount
                        var goods_List = new Models.Bills();
                        goods_List.Id = DbNullSafe.ToInt32(reader["ID"]);
                        goods_List.Billnumber = DbNullSafe.ToInt32(reader["billnumber"]);
                        goods_List.Billcost = DbNullSafe.ToDouble(reader["billcost"]);
                        goods_List.Time = DbNullSafe.ToStringSafe(reader["time"]);
                        goods_List.Datex = DbNullSafe.ToStringSafe(reader["datex"]);
                        goods_List.Ownername = DbNullSafe.ToStringSafe(reader["ownername"]);
                        goods_List.Ownerid = DbNullSafe.ToStringSafe(reader["ownerid"]);
                        goods_List.Ownernumber = DbNullSafe.ToStringSafe(reader["ownernumber"]);
                        goods_List.Paid = DbNullSafe.ToDouble(reader["paid"]);
                        goods_List.Remain = DbNullSafe.ToDouble(reader["remain"]);
                        goods_List.Earned = DbNullSafe.ToDouble(reader["earned"]);
                        goods_List.Tax = DbNullSafe.ToDouble(reader["tax"]);
                        goods_List.Discount = DbNullSafe.ToDouble(reader["discount"]);
                        goods_List.Details = DbNullSafe.ToStringSafe(reader["Details"]);
                        goods_List.CustomerId = DbNullSafe.ToNullableInt32(reader["CustomerId"]);
                        goods_List.IsCurrent = DbNullSafe.ToBool(reader["IsCurrent"]);
                        goods_List.RevisionSuffix = DbNullSafe.ToStringSafe(reader["RevisionSuffix"]);
                        if (goods_List.RevisionSuffix == "") goods_List.RevisionSuffix = null;
                        goods_List.DiscountPercent = DbNullSafe.ToDouble(reader["DiscountPercent"]);
                        bills.Add(goods_List);
                      
                    }
                    return bills;
                }
            }
        }
              // Sales-history-per-customer, for the pharma stock-check feature's
        // detail drill-down. Relies on bills.CustomerId (see
        // DatabaseBootstrapper) rather than string-matching
        // Ownername/Ownerid, which is fragile if a name gets re-typed
        // slightly differently between visits.
        public List<Models.Bills> ReadBillsByCustomer(string TableName, int CustomerId)
        {
            List<Models.Bills> bills = new List<Models.Bills>();
            string readString = "SELECT * FROM " + TableName + " WHERE CustomerId = @customerid";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(readString, conn))
                {
                    cmd.Parameters.AddWithValue("@customerid", CustomerId);
                    IDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var goods_List = new Models.Bills();
                        goods_List.Id = DbNullSafe.ToInt32(reader["ID"]);
                        goods_List.Billnumber = DbNullSafe.ToInt32(reader["billnumber"]);
                        goods_List.Billcost = DbNullSafe.ToDouble(reader["billcost"]);
                        goods_List.Time = DbNullSafe.ToStringSafe(reader["time"]);
                        goods_List.Datex = DbNullSafe.ToStringSafe(reader["datex"]);
                        goods_List.Ownername = DbNullSafe.ToStringSafe(reader["ownername"]);
                        goods_List.Ownerid = DbNullSafe.ToStringSafe(reader["ownerid"]);
                        goods_List.Ownernumber = DbNullSafe.ToStringSafe(reader["ownernumber"]);
                        goods_List.Paid = DbNullSafe.ToDouble(reader["paid"]);
                        goods_List.Remain = DbNullSafe.ToDouble(reader["remain"]);
                        goods_List.Earned = DbNullSafe.ToDouble(reader["earned"]);
                        goods_List.Tax = DbNullSafe.ToDouble(reader["tax"]);
                        goods_List.Discount = DbNullSafe.ToDouble(reader["discount"]);
                        goods_List.Details = DbNullSafe.ToStringSafe(reader["Details"]);
                        goods_List.CustomerId = DbNullSafe.ToNullableInt32(reader["CustomerId"]);
                        goods_List.IsCurrent = DbNullSafe.ToBool(reader["IsCurrent"]);
                        goods_List.RevisionSuffix = DbNullSafe.ToStringSafe(reader["RevisionSuffix"]);
                        if (goods_List.RevisionSuffix == "") goods_List.RevisionSuffix = null;
                        goods_List.DiscountPercent = DbNullSafe.ToDouble(reader["DiscountPercent"]);
                        bills.Add(goods_List);
                    }
                    return bills;
                }
            }
        }

        // Everything below added 2026-08-27 for item #6 (Bills view on
        // Checkout + delete-line/delete-whole-bill with reversal).

        public bool DeleteBillById(string TableName, int Id)
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

        // Re-totals a bill after one of its line items is deleted (a whole
        // bill delete just removes the bills row entirely via
        // DeleteBillById above -- this is only for the "delete one product
        // from the bill, the rest stays" case). Billcost/Paid/Remain/Earned
        // are the four fields that actually shift when a line disappears --
        // Time/Datex/Ownername/etc. describe the bill as a whole and don't
        // change just because it now has one fewer item on it.
        public bool UpdateBillAmounts(string TableName, int Id, double Billcost, double Paid, double Remain, double Earned)
        {
            string updateString = "UPDATE " + TableName + " SET Billcost = @billcost, Paid = @paid, Remain = @remain, Earned = @earned WHERE ID = @id";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(updateString, conn))
                {
                    cmd.Parameters.AddWithValue("@billcost", Billcost);
                    cmd.Parameters.AddWithValue("@paid", Paid);
                    cmd.Parameters.AddWithValue("@remain", Remain);
                    cmd.Parameters.AddWithValue("@earned", Earned);
                    cmd.Parameters.AddWithValue("@id", Id);
                    cmd.ExecuteNonQuery();
                    cmd.Dispose();
                    return true;
                }
            }
        }

        // Everything below added 2026-08-28 for receipt revisioning (see
        // DatabaseBootstrapper's matching comment) -- returning a product
        // from a bill no longer rewrites that bill's row (UpdateBillAmounts
        // above still exists but BillsBrowserViewModel's return flow no
        // longer calls it); instead the OLD bill row is flipped to
        // IsCurrent = false via SetBillCurrent and a brand-new row is
        // inserted (via InsertBills' IsCurrent/RevisionSuffix parameters
        // above) to replace it as the receipt's current version.

        /// <summary>
        /// Marks a bill row as no longer the current version of its receipt
        /// (false, when a return creates a replacement row) or, in
        /// principle, back to current (true) -- only the false direction is
        /// actually used today, by BillsBrowserViewModel superseding the
        /// bill a return was made against.
        /// </summary>
        public bool SetBillCurrent(string TableName, int Id, bool IsCurrent)
        {
            string updateString = "UPDATE " + TableName + " SET IsCurrent = @iscurrent WHERE ID = @id";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(updateString, conn))
                {
                    cmd.Parameters.AddWithValue("@iscurrent", IsCurrent ? 1 : 0);
                    cmd.Parameters.AddWithValue("@id", Id);
                    cmd.ExecuteNonQuery();
                    cmd.Dispose();
                    return true;
                }
            }
        }

        /// <summary>
        /// Every bills row sharing one Billnumber -- the original plus
        /// every return-revision made against it -- oldest first. Used by
        /// BillsBrowserViewModel to work out the next revision suffix
        /// (count of rows that already have a non-null RevisionSuffix, + 1)
        /// and to find the receipt's current row when someone opens a
        /// superseded one from the Bills list.
        /// </summary>
        public List<Models.Bills> ReadBillRevisions(string TableName, int Billnumber)
        {
            List<Models.Bills> bills = new List<Models.Bills>();
            string readString = "SELECT * FROM " + TableName + " WHERE Billnumber = @billnumber ORDER BY ID ASC";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(readString, conn))
                {
                    cmd.Parameters.AddWithValue("@billnumber", Billnumber);
                    IDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var goods_List = new Models.Bills();
                        goods_List.Id = DbNullSafe.ToInt32(reader["ID"]);
                        goods_List.Billnumber = DbNullSafe.ToInt32(reader["billnumber"]);
                        goods_List.Billcost = DbNullSafe.ToDouble(reader["billcost"]);
                        goods_List.Time = DbNullSafe.ToStringSafe(reader["time"]);
                        goods_List.Datex = DbNullSafe.ToStringSafe(reader["datex"]);
                        goods_List.Ownername = DbNullSafe.ToStringSafe(reader["ownername"]);
                        goods_List.Ownerid = DbNullSafe.ToStringSafe(reader["ownerid"]);
                        goods_List.Ownernumber = DbNullSafe.ToStringSafe(reader["ownernumber"]);
                        goods_List.Paid = DbNullSafe.ToDouble(reader["paid"]);
                        goods_List.Remain = DbNullSafe.ToDouble(reader["remain"]);
                        goods_List.Earned = DbNullSafe.ToDouble(reader["earned"]);
                        goods_List.Tax = DbNullSafe.ToDouble(reader["tax"]);
                        goods_List.Discount = DbNullSafe.ToDouble(reader["discount"]);
                        goods_List.Details = DbNullSafe.ToStringSafe(reader["Details"]);
                        goods_List.CustomerId = DbNullSafe.ToNullableInt32(reader["CustomerId"]);
                        goods_List.IsCurrent = DbNullSafe.ToBool(reader["IsCurrent"]);
                        goods_List.RevisionSuffix = DbNullSafe.ToStringSafe(reader["RevisionSuffix"]);
                        if (goods_List.RevisionSuffix == "") goods_List.RevisionSuffix = null;
                        goods_List.DiscountPercent = DbNullSafe.ToDouble(reader["DiscountPercent"]);
                        bills.Add(goods_List);
                    }
                    return bills;
                }
            }
        }

    }
}
