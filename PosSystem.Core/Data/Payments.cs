using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;

namespace PosSystem.Core.Data
{
    /// <summary>
    /// CRUD for the `payments` table (see DatabaseBootstrapper for the
    /// schema and Models.Payment's doc comment for the overall design).
    /// Append-only except for MarkReverted flipping IsReverted — a
    /// reverted row is never deleted, so the history stays a complete,
    /// honest record of everything that happened, including undos.
    /// </summary>
    public class Payments
    {
        Server server = new Server();

        public void InsertPayment(int CustomerId, string Type, double Amount, double AppliedToRemain,
            double AppliedToCredit, double PreviousPaid, double PreviousRemain, double PreviousCredit,
            string Notes, string PaymentDate, string PaymentTime)
        {
            string insertString = "insert into payments " +
                "(CustomerId, Type, Amount, AppliedToRemain, AppliedToCredit, PreviousPaid, PreviousRemain, PreviousCredit, IsReverted, Notes, PaymentDate, PaymentTime) " +
                "VALUES (@customerid, @type, @amount, @appliedremain, @appliedcredit, @prevpaid, @prevremain, @prevcredit, 0, @notes, @date, @time)";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(insertString, conn))
                {
                    cmd.Parameters.AddWithValue("@customerid", CustomerId);
                    cmd.Parameters.AddWithValue("@type", Type);
                    cmd.Parameters.AddWithValue("@amount", Amount);
                    cmd.Parameters.AddWithValue("@appliedremain", AppliedToRemain);
                    cmd.Parameters.AddWithValue("@appliedcredit", AppliedToCredit);
                    cmd.Parameters.AddWithValue("@prevpaid", PreviousPaid);
                    cmd.Parameters.AddWithValue("@prevremain", PreviousRemain);
                    cmd.Parameters.AddWithValue("@prevcredit", PreviousCredit);
                    cmd.Parameters.AddWithValue("@notes", Notes ?? "");
                    cmd.Parameters.AddWithValue("@date", PaymentDate ?? "");
                    cmd.Parameters.AddWithValue("@time", PaymentTime ?? "");
                    cmd.ExecuteNonQuery();
                    cmd.Dispose();
                }
            }
        }

        // Ordered newest-first by ID (autoincrement), same reasoning as
        // StockChecks.ReadByCustomer — PaymentDate is text in "dd/MM/yyyy"
        // form and doesn't sort correctly as a string.
        public List<Models.Payment> ReadByCustomer(int CustomerId)
        {
            var results = new List<Models.Payment>();
            string readString = "SELECT * FROM payments WHERE CustomerId = @customerid ORDER BY ID DESC";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(readString, conn))
                {
                    cmd.Parameters.AddWithValue("@customerid", CustomerId);
                    IDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        results.Add(ReadRow(reader));
                    }
                    return results;
                }
            }
        }

        public Models.Payment GetById(int Id)
        {
            string readString = "SELECT * FROM payments WHERE ID = @id";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(readString, conn))
                {
                    cmd.Parameters.AddWithValue("@id", Id);
                    IDataReader reader = cmd.ExecuteReader();
                    if (reader.Read()) return ReadRow(reader);
                    return null;
                }
            }
        }

        // Reverting never deletes the row — see class doc comment. The
        // caller (CustomerDetailViewModel.RevertPayment) is responsible
        // for applying this payment's inverse effect to the customer's
        // Paid/Remain/CreditOwed balances before/after calling this; this
        // method only flips the flag so it can't be reverted a second time
        // and so the history UI can grey out its Revert button.
        public bool MarkReverted(int Id)
        {
            string updateString = "UPDATE payments SET IsReverted = 1 WHERE ID = @id";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(updateString, conn))
                {
                    cmd.Parameters.AddWithValue("@id", Id);
                    cmd.ExecuteNonQuery();
                    cmd.Dispose();
                    return true;
                }
            }
        }

        private static Models.Payment ReadRow(IDataReader reader)
        {
            return new Models.Payment
            {
                Id = DbNullSafe.ToInt32(reader["ID"]),
                CustomerId = DbNullSafe.ToInt32(reader["CustomerId"]),
                Type = DbNullSafe.ToStringSafe(reader["Type"]),
                Amount = DbNullSafe.ToDouble(reader["Amount"]),
                AppliedToRemain = DbNullSafe.ToDouble(reader["AppliedToRemain"]),
                AppliedToCredit = DbNullSafe.ToDouble(reader["AppliedToCredit"]),
                PreviousPaid = DbNullSafe.ToDouble(reader["PreviousPaid"]),
                PreviousRemain = DbNullSafe.ToDouble(reader["PreviousRemain"]),
                PreviousCredit = DbNullSafe.ToDouble(reader["PreviousCredit"]),
                IsReverted = DbNullSafe.ToBool(reader["IsReverted"]),
                Notes = DbNullSafe.ToStringSafe(reader["Notes"]),
                PaymentDate = DbNullSafe.ToStringSafe(reader["PaymentDate"]),
                PaymentTime = DbNullSafe.ToStringSafe(reader["PaymentTime"])
            };
        }
    }
}
