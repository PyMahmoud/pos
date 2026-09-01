using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PosSystem.Core.Data
{
    public class Customers
    {
        Server server = new Server();

        //private int id;
        //private string ownername;
        //private string ownerid;
        //private string ownernumber;
        //private double paid;
        //private double remain;

        public void InsertCustomers(string TableName, string Ownername, string Ownerid, string Ownernumber, double Paid, double Remain)
        {
            string insertString = "insert into " + TableName + "(Ownername ,Ownerid ,Ownernumber ,Paid ,Remain ) VALUES (@ownername , @ownerid , @ownernumber  , @paid ,@remain)";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(insertString, conn))
                {
                    cmd.Parameters.AddWithValue("@ownername", Ownername);
                    cmd.Parameters.AddWithValue("@ownerid", Ownerid);
                    cmd.Parameters.AddWithValue("@ownernumber", Ownernumber);
                    cmd.Parameters.AddWithValue("@paid", Paid);
                    cmd.Parameters.AddWithValue("@remain", Remain);
                 
                    cmd.ExecuteNonQuery();
                    cmd.Dispose();
                }
            }
        }
        public List<Models.Customers> ReadCustomers_Range(string TableName, string FieldName, string FieldValue, string FieldValue2)
        {

            List<Models.Customers> goods = new List<Models.Customers>();
            string readString = "SELECT * FROM " + TableName + " WHERE " + FieldName + " BETWEEN @fieldvalue AND @fieldvalue2 ";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(readString, conn))
                {
                    cmd.Parameters.AddWithValue("@fieldvalue", FieldValue);
                    cmd.Parameters.AddWithValue("@fieldvalue2", FieldValue2);
                    IDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var goods_List = new Models.Customers();
                        goods_List.Id = DbNullSafe.ToInt32(reader["ID"]);
                        goods_List.Ownername = DbNullSafe.ToStringSafe(reader["Ownername"]);
                        goods_List.Ownerid = DbNullSafe.ToStringSafe(reader["Ownerid"]);
                        goods_List.Ownernumber = DbNullSafe.ToStringSafe(reader["Ownernumber"]);
                      
                        goods_List.Paid = DbNullSafe.ToDouble(reader["Paid"]);
                        goods_List.Remain = DbNullSafe.ToDouble(reader["Remain"]);
                        goods_List.CreditOwed = DbNullSafe.ToDouble(reader["CreditOwed"]);
                        goods_List.DiscountPercent = DbNullSafe.ToDouble(reader["DiscountPercent"]);
                      
                        //goods_List.Details = reader["Details"].ToString();
                        goods.Add(goods_List);

                    }
                    return goods;
                }
            }
        }
        public List<Models.Customers> ReadCustomers(string TableName)
        {

            List<Models.Customers> goods = new List<Models.Customers>();
            string readString = "SELECT * FROM " + TableName + " ";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(readString, conn))
                {
                    IDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var goods_List = new Models.Customers();
                        goods_List.Id = DbNullSafe.ToInt32(reader["ID"]);
                        goods_List.Ownername = DbNullSafe.ToStringSafe(reader["Ownername"]);
                        goods_List.Ownerid = DbNullSafe.ToStringSafe(reader["Ownerid"]);
                        goods_List.Ownernumber = DbNullSafe.ToStringSafe(reader["Ownernumber"]);

                        goods_List.Paid = DbNullSafe.ToDouble(reader["Paid"]);
                        goods_List.Remain = DbNullSafe.ToDouble(reader["Remain"]);
                        goods_List.CreditOwed = DbNullSafe.ToDouble(reader["CreditOwed"]);
                        goods_List.DiscountPercent = DbNullSafe.ToDouble(reader["DiscountPercent"]);

                        //goods_List.Details = reader["Details"].ToString();
                        goods.Add(goods_List);

                    }
                    return goods;
                }
            }
        }

        public string ReadBillnumber(string TableName, string Fieldname)
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
                        returned_value = reader["ID"].ToString();

                    }
                    return returned_value;
                }
            }
        }
        public bool UpdateCustomers(string TableName, int ID, string Ownername, string Ownerid, string Ownernumber, double Paid, double Remain)
        {
            string UpdateString = "UPDATE " + TableName + " SET (ID ,Ownername ,Ownerid ,Ownernumber ,Paid ,Remain ) = (@id ,@ownername , @ownerid , @ownernumber  , @paid ,@remain) WHERE ID =@id";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(UpdateString, conn))
                {
                    cmd.Parameters.AddWithValue("@id", ID);
                    cmd.Parameters.AddWithValue("@ownername", Ownername);
                    cmd.Parameters.AddWithValue("@ownerid", Ownerid);
                    cmd.Parameters.AddWithValue("@ownernumber", Ownernumber);
                    cmd.Parameters.AddWithValue("@paid", Paid);
                    cmd.Parameters.AddWithValue("@remain", Remain);
                    cmd.ExecuteNonQuery();
                    cmd.Dispose();
                    return true;
                }
            }
        }

        // Added for "money we owe the customer" / payment revert
        // (2026-08-31) -- a separate method rather than widening
        // UpdateCustomers above, so every existing call site (which knows
        // nothing about CreditOwed) keeps compiling and behaving exactly as
        // before. Used by CustomersViewModel.RecordPayment, 
        // CustomerDetailViewModel.AddManualCredit, and 
        // CustomerDetailViewModel.RevertPayment -- anywhere Paid/Remain/
        // CreditOwed need to move together as one atomic balance update.
        public bool UpdateCustomerBalance(string TableName, int ID, string Ownername, string Ownerid, string Ownernumber, double Paid, double Remain, double CreditOwed)
        {
            string UpdateString = "UPDATE " + TableName + " SET Ownername=@ownername, Ownerid=@ownerid, Ownernumber=@ownernumber, Paid=@paid, Remain=@remain, CreditOwed=@creditowed WHERE ID=@id";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(UpdateString, conn))
                {
                    cmd.Parameters.AddWithValue("@id", ID);
                    cmd.Parameters.AddWithValue("@ownername", Ownername);
                    cmd.Parameters.AddWithValue("@ownerid", Ownerid);
                    cmd.Parameters.AddWithValue("@ownernumber", Ownernumber);
                    cmd.Parameters.AddWithValue("@paid", Paid);
                    cmd.Parameters.AddWithValue("@remain", Remain);
                    cmd.Parameters.AddWithValue("@creditowed", CreditOwed);
                    cmd.ExecuteNonQuery();
                    cmd.Dispose();
                    return true;
                }
            }
        }

        // Added for per-customer default discount percentage (2026-09-01)
        // -- a separate, narrow method rather than widening UpdateCustomers/
        // UpdateCustomerBalance above, same reasoning as those two: nothing
        // else about the customer changes when their default discount is
        // edited from the customer detail page, so every other call site
        // stays untouched.
        public bool UpdateCustomerDiscount(string TableName, int ID, double DiscountPercent)
        {
            string UpdateString = "UPDATE " + TableName + " SET DiscountPercent=@discountpercent WHERE ID=@id";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(UpdateString, conn))
                {
                    cmd.Parameters.AddWithValue("@id", ID);
                    cmd.Parameters.AddWithValue("@discountpercent", DiscountPercent);
                    cmd.ExecuteNonQuery();
                    cmd.Dispose();
                    return true;
                }
            }
        }

    }
}
