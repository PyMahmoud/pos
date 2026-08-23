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
        public void InsertBills(string TableName,int ID, int Billnumber, double Billcost, string Time, string Datex, string Ownername, string Ownerid, string Ownernumber, double Paid, double Remain, double Earned, double Tax, double Discount, string Details, int? CustomerId = null)
        {
            string insertString = "insert into " + TableName + "(ID ,Billnumber ,Billcost ,Time ,Datex ,Ownername ,Ownerid  , Ownernumber ,  Paid ,Remain ,Earned , Tax ,Discount , Details, CustomerId) VALUES (@id ,@billnumber , @billcost , @time , @datex , @ownername ,@ownerid ,@pwnernumber , @paid ,@remain , @earned ,@tax ,@discount , @details, @customerid)";
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
                        bills.Add(goods_List);
                    }
                    return bills;
                }
            }
        }

    }
}
