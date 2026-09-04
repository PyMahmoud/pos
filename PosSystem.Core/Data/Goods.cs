using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PosSystem.Core.Data
{
    public class Goods
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
        public void InsertGoods(string TableName, string Name, string Category, double Quantity, double Cost, double Price, string Type, string Barcode, double Earned, string Datex, string Datee)
        {
            string insertString = "insert into " + TableName + "(Name ,Category ,Quantity ,Cost ,Price ,Type ,Barcode , Earned , Datex , Datee) VALUES (@name , @category , @quantity , @cost , @price ,@type ,@barcode ,@earned ,@datex , @datee)";
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
                    cmd.Parameters.AddWithValue("@barcode", Barcode);
                    cmd.Parameters.AddWithValue("@earned", Earned);
                    cmd.Parameters.AddWithValue("@datex", Datex);
                    cmd.Parameters.AddWithValue("@datee", Datee);
                   
                    cmd.ExecuteNonQuery();
                    cmd.Dispose();
                }
            }
        }
        // Added 2026-09-03 for Inventory's staged-edits/undo-redo/Save-
        // Changes feature -- InsertGoods above (immediate-write era) never
        // needed to know the assigned ID, since nothing downstream in the
        // same operation ever referenced the new row again. Now it does:
        // InventoryViewModel stages an Add as a plain in-memory InventoryRow
        // with a temporary (negative, never persisted) placeholder ID, and
        // only finds out the real one at Save Changes time -- SQLiteConnection
        // .LastInsertRowId (same connection, right after the INSERT, before
        // it's closed) is the standard, reliable way to get that back without
        // a second round-trip query. See InventoryViewModel's class doc
        // comment on the staging model for the full picture.
        // Signature widened 2026-09-04 to accept DiscountPercent -- see
        // this method's original 2026-09-03 doc comment above for why the
        // ID needs to come back at all. Needed here (not just on
        // UpdateGoodsById below) for a real edge case: a product can be
        // Added AND given a discount (via the bulk "Add Discounts" button
        // or the Discounts page) in the SAME pending session, before ever
        // clicking Save Changes -- SaveChanges()'s field-update loop only
        // ever touches rows already present in the pre-Save baseline
        // snapshot (see that method's own comment), so a brand-new row's
        // DiscountPercent has no later UPDATE that would ever write it;
        // it has to go in on the initial INSERT or it's silently lost.
        public int InsertGoodsReturningId(string TableName, string Name, string Category, double Quantity, double Cost, double Price, string Type, string Barcode, double Earned, string Datex, string Datee, double DiscountPercent)
        {
            string insertString = "insert into " + TableName + "(Name ,Category ,Quantity ,Cost ,Price ,Type ,Barcode , Earned , Datex , Datee , DiscountPercent) VALUES (@name , @category , @quantity , @cost , @price ,@type ,@barcode ,@earned ,@datex , @datee , @discountpercent)";
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
                    cmd.Parameters.AddWithValue("@barcode", Barcode);
                    cmd.Parameters.AddWithValue("@earned", Earned);
                    cmd.Parameters.AddWithValue("@datex", Datex);
                    cmd.Parameters.AddWithValue("@datee", Datee);
                    cmd.Parameters.AddWithValue("@discountpercent", DiscountPercent);

                    cmd.ExecuteNonQuery();
                    return (int)conn.LastInsertRowId;
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
        public List<Models.Goods> ReadGoodsPic(string TableName)
        {

            List<Models.Goods> goods = new List<Models.Goods>();
            string readString = "SELECT * FROM " + TableName + " ";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(readString, conn))
                {
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
        public List<Models.Goods> ReadGoodsPic(string TableName , string Category)
        {

            List<Models.Goods> goods = new List<Models.Goods>();
            string readString = "SELECT * FROM " + TableName + " Where Category=@category ORDER BY Name ASC";
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
                        goods_List.Category = reader["Category"].ToString();
                        goods_List.Quantity = Convert.ToDouble(reader["Quantity"]);
                        goods_List.Cost = Convert.ToDouble(reader["Cost"]);
                        goods_List.Price = Convert.ToDouble(reader["Price"]);
                        goods_List.Type = reader["Type"].ToString();
                        goods_List.Datex = reader["Datex"].ToString();
                        goods_List.Datee = reader["Datee"].ToString();
                        //goods_List.Type = reader["Type"].ToString();
                        goods_List.Barcode = reader["Barcode"].ToString();
                        goods_List.Earned = Convert.ToDouble(reader["Earned"]);
                        //goods_List.Details = reader["Details"].ToString();
                        //if (!Convert.IsDBNull(reader["Image"]))
                        //{
                        //    goods_List.Image = (byte[])(reader["Image"]); //TODO
                        //}
                        goods.Add(goods_List);

                    }
                    return goods;
                }
            }
        }
        public List<Models.Goods> ReadGoodsPic(string TableName, string FieldName , string FieldValue)
        {

            List<Models.Goods> goods = new List<Models.Goods>();
            string readString = "SELECT * FROM " + TableName + " Where "+ FieldName + "=@fieldvalue ORDER BY Name ASC";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(readString, conn))
                {
                    cmd.Parameters.AddWithValue("@fieldvalue", FieldValue);
                    IDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var goods_List = new Models.Goods();
                        goods_List.Id = Convert.ToInt32(reader["ID"]);
                        goods_List.Name = reader["Name"].ToString();
                        goods_List.Category = reader["Category"].ToString();
                        goods_List.Quantity = Convert.ToDouble(reader["Quantity"]);
                        goods_List.Cost = Convert.ToDouble(reader["Cost"]);
                        goods_List.Price = Convert.ToDouble(reader["Price"]);
                        goods_List.Type = reader["Type"].ToString();
                        goods_List.Datex = reader["Datex"].ToString();
                        goods_List.Datee = reader["Datee"].ToString();
                        //goods_List.Type = reader["Type"].ToString();
                        goods_List.Barcode = reader["Barcode"].ToString();
                        goods_List.Earned = Convert.ToDouble(reader["Earned"]);
                        //goods_List.Details = reader["Details"].ToString();
                        //if (!Convert.IsDBNull(reader["Image"]))
                        //{
                        //    goods_List.Image = (byte[])(reader["Image"]); //TODO
                        //}
                        goods.Add(goods_List);

                    }
                    return goods;
                }
            }
        }
        public List<Models.Goods> ReadGoodsPic_Like(string TableName, string FieldName, string FieldValue)
        {

            List<Models.Goods> goods = new List<Models.Goods>();
            string readString = "SELECT * FROM " + TableName + " Where " + FieldName + " like @fieldvalue || '%' ORDER BY Name ASC";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(readString, conn))
                {
                    cmd.Parameters.AddWithValue("@fieldvalue", FieldValue);
                    IDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var goods_List = new Models.Goods();
                        goods_List.Id = Convert.ToInt32(reader["ID"]);
                        goods_List.Name = reader["Name"].ToString();
                        goods_List.Category = reader["Category"].ToString();
                        goods_List.Quantity = Convert.ToDouble(reader["Quantity"]);
                        goods_List.Cost = Convert.ToDouble(reader["Cost"]);
                        goods_List.Price = Convert.ToDouble(reader["Price"]);
                        goods_List.Type = reader["Type"].ToString();
                        goods_List.Datex = reader["Datex"].ToString();
                        goods_List.Datee = reader["Datee"].ToString();
                        //goods_List.Type = reader["Type"].ToString();
                        goods_List.Barcode = reader["Barcode"].ToString();
                        goods_List.Earned = Convert.ToDouble(reader["Earned"]);
                        //goods_List.Details = reader["Details"].ToString();
                        //if (!Convert.IsDBNull(reader["Image"]))
                        //{
                        //    goods_List.Image = (byte[])(reader["Image"]); //TODO
                        //}
                        goods.Add(goods_List);

                    }
                    return goods;
                }
            }
        }
        public List<Models.Goods> ReadAllGoodsQuantity(string TableName)
        {

            List<Models.Goods> goods = new List<Models.Goods>();
            string readString = "SELECT * FROM " + TableName + " ORDER BY Quantity ASC";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(readString, conn))
                {

                    IDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var goods_List = new Models.Goods();
                        goods_List.Id = Convert.ToInt32(reader["ID"]);
                        goods_List.Name = reader["Name"].ToString();
                        goods_List.Category = reader["Category"].ToString();
                        goods_List.Quantity = Convert.ToDouble(reader["Quantity"]);
                        goods_List.Cost = Convert.ToDouble(reader["Cost"]);
                        goods_List.Price = Convert.ToDouble(reader["Price"]);
                        goods_List.Type = reader["Type"].ToString();
                        goods_List.Datex = reader["Datex"].ToString();
                        goods_List.Datee = reader["Datee"].ToString();
                        goods_List.Barcode = reader["Barcode"].ToString();
                        goods_List.Earned = Convert.ToDouble(reader["Earned"]);
                        // DiscountPercent added 2026-09-04 (Inventory's
                        // Discounts feature) -- DbNullSafe.ToDouble, not a
                        // raw Convert.ToDouble, since a database that
                        // hasn't been through DatabaseBootstrapper's
                        // backfill yet (or somehow missed a row) would
                        // otherwise crash this entire read on a NULL cell,
                        // same reasoning as every other DbNullSafe usage in
                        // this file's newer methods.
                        goods_List.DiscountPercent = DbNullSafe.ToDouble(reader["DiscountPercent"]);
                        //goods_List.Details = reader["Details"].ToString();
                        //if (!Convert.IsDBNull(reader["Image"]))
                        //{
                        //    goods_List.Image = (byte[])(reader["Image"]); //TODO
                        //}
                        goods.Add(goods_List);

                    }
                    return goods;
                }
            }
        }
        public List<Models.Goods> ReadAllGoodsPic(string TableName)
        {

            List<Models.Goods> goods = new List<Models.Goods>();
            string readString = "SELECT * FROM " + TableName + " ORDER BY Name ASC";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(readString, conn))
                {
                   
                    IDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var goods_List = new Models.Goods();
                        goods_List.Id = Convert.ToInt32(reader["ID"]);
                        goods_List.Name = reader["Name"].ToString();
                        goods_List.Category = reader["Category"].ToString();
                        goods_List.Quantity = Convert.ToDouble(reader["Quantity"]);
                        goods_List.Cost = Convert.ToDouble(reader["Cost"]);
                        goods_List.Price = Convert.ToDouble(reader["Price"]);
                        goods_List.Type = reader["Type"].ToString();
                        goods_List.Datex = reader["Datex"].ToString();
                        goods_List.Datee = reader["Datee"].ToString();
                        goods_List.Barcode = reader["Barcode"].ToString();
                        goods_List.Earned = Convert.ToDouble(reader["Earned"]);
                        //goods_List.Details = reader["Details"].ToString();
                        //if (!Convert.IsDBNull(reader["Image"]))
                        //{
                        //    goods_List.Image = (byte[])(reader["Image"]); //TODO
                        //}
                        goods.Add(goods_List);

                    }
                    return goods;
                }
            }
        }
        public List<Models.GoodsR> ReadGoodsRPic(string TableName, string Category)
        {

            List<Models.GoodsR> goods = new List<Models.GoodsR>();
            string readString = "SELECT * FROM " + TableName + " Where Category=@category ORDER BY Name ASC";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(readString, conn))
                {
                    cmd.Parameters.AddWithValue("@category", Category);
                    IDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var goods_List = new Models.GoodsR();
                        goods_List.Id = Convert.ToInt32(reader["ID"]);
                        goods_List.Name = reader["Name"].ToString();
                        goods_List.Category = reader["Category"].ToString();
                        goods_List.Quantity = Convert.ToDouble(reader["Quantity"]);
                        goods_List.Cost = Convert.ToDouble(reader["Cost"]);
                        goods_List.Price = Convert.ToDouble(reader["Price"]);
                        goods_List.Type = reader["Type"].ToString();
                        goods_List.Datex = reader["Datex"].ToString();
                        goods_List.Datee = reader["Datee"].ToString();
                        //goods_List.Type = reader["Type"].ToString();
                        goods_List.Barcode = reader["Barcode"].ToString();
                        goods_List.Earned = Convert.ToDouble(reader["Earned"]);
                        //goods_List.Details = reader["Details"].ToString();
                        //if (!Convert.IsDBNull(reader["Image"]))
                        //{
                        //    goods_List.Image = (byte[])(reader["Image"]); //TODO
                        //}

                        goods.Add(goods_List);

                    }
                    return goods;
                }
            }
        }
        public List<Models.GoodsR> ReadAllGoodsRPic(string TableName)
        {

            List<Models.GoodsR> goods = new List<Models.GoodsR>();
            string readString = "SELECT * FROM " + TableName + " ORDER BY Name ASC";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(readString, conn))
                {

                    IDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var goods_List = new Models.GoodsR();
                        goods_List.Id = Convert.ToInt32(reader["ID"]);
                        goods_List.Name = reader["Name"].ToString();
                        goods_List.Category = reader["Category"].ToString();
                        goods_List.Quantity = Convert.ToDouble(reader["Quantity"]);
                        goods_List.Cost = Convert.ToDouble(reader["Cost"]);
                        goods_List.Price = Convert.ToDouble(reader["Price"]);
                        goods_List.Type = reader["Type"].ToString();
                        goods_List.Datex = reader["Datex"].ToString();
                        goods_List.Datee = reader["Datee"].ToString();
                        goods_List.Barcode = reader["Barcode"].ToString();
                        goods_List.Earned = Convert.ToDouble(reader["Earned"]);
                        //goods_List.Details = reader["Details"].ToString();
                        //if (!Convert.IsDBNull(reader["Image"]))
                        //{
                        //    goods_List.Image = (byte[])(reader["Image"]); //TODO
                        //}
                        goods.Add(goods_List);

                    }
                    return goods;
                }
            }
        }
        public double ReadGoodsQuantity(string TableName ,string Barcode)
        {

            double _quantity = 0;
            string readString = "SELECT * FROM " + TableName + " WHERE Barcode =@barcode";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(readString, conn))
                {
                    cmd.Parameters.AddWithValue("@barcode", Barcode);
                    IDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        _quantity = Convert.ToDouble(reader["Quantity"]);
                    }
                    return _quantity;
                }
            }
        }
        //Name ,Category ,Quantity ,Cost ,Price ,Type , Image
        public bool UpdateGoods(string TableName , int ID , string Name , string Category ,double Quantity , double Cost , double Price , string Type , string Barcode , double Earned )
        {
            string UpdateString = "UPDATE " + TableName + " SET (ID ,Name ,Category ,Quantity ,Cost ,Price ,Type  ,Earned  ) = (@id ,@name , @category , @quantity , @cost , @price ,@type  ,@earned  ) WHERE Barcode =@barcode";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(UpdateString, conn))
                {
                    cmd.Parameters.AddWithValue("@id", ID);
                    cmd.Parameters.AddWithValue("@name", Name);
                    cmd.Parameters.AddWithValue("@category", Category);
                    cmd.Parameters.AddWithValue("@quantity", Quantity);
                    cmd.Parameters.AddWithValue("@cost", Cost);
                    cmd.Parameters.AddWithValue("@price", Price);
                    cmd.Parameters.AddWithValue("@type", Type);
                  
                    cmd.Parameters.AddWithValue("@earned", Earned);
                    
                 
                    cmd.Parameters.AddWithValue("@barcode", Barcode);
                    cmd.ExecuteNonQuery();
                    cmd.Dispose();
                    return true;
                }
            }
        }
        public bool UpdateGoodCount(string TableName  , string Barcode, double Quantity)
        {
        
            string UpdateString = "UPDATE " + TableName + " SET (Quantity) = (@quantity) WHERE Barcode =@barcode";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(UpdateString, conn))
                {
                    cmd.Parameters.AddWithValue("@quantity", Quantity);
                    cmd.Parameters.AddWithValue("@barcode", Barcode);
                    cmd.ExecuteNonQuery();
                    cmd.Dispose();
                    return true;
                }
            }

          
        }
        public bool RemoveGoods(string TableName, string Barcode)
        {
            string RemoveString = "DELETE FROM " + TableName + " WHERE Barcode =@barcode";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(RemoveString, conn))
                {
                    cmd.Parameters.AddWithValue("@barcode", Barcode);
                    cmd.ExecuteNonQuery();
                    cmd.Dispose();
                    return true;
                }
            }
        }

        // Added for Inventory's Add Product feature (barcode is now
        // optional on new products). Every existing write path in this
        // class — UpdateGoods, UpdateGoodCount, RemoveGoods above — keys
        // off Barcode in its WHERE clause, which silently breaks the
        // instant two products can legitimately share the same Barcode
        // value (namely "", once barcode-less products exist): a
        // Barcode-keyed UPDATE/DELETE would match every barcode-less
        // product at once instead of the one intended. ID is the real
        // primary key (INTEGER PRIMARY KEY AUTOINCREMENT) and always
        // unique regardless of Barcode — UpdateGoodCountById is the
        // correct replacement for any caller whose product might not have
        // a barcode. InventoryViewModel.AdjustQuantity and
        // CheckoutViewModel.CompleteSale both switched to this for exactly
        // that reason; the Barcode-keyed methods above are left as-is
        // (unused by those two call sites now, but not deleted — no
        // evidence anything else depends on removing them, and this
        // session has no way to verify a wider removal is safe).
        public bool UpdateGoodCountById(string TableName, int Id, double Quantity)
        {
            string UpdateString = "UPDATE " + TableName + " SET Quantity = @quantity WHERE ID = @id";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(UpdateString, conn))
                {
                    cmd.Parameters.AddWithValue("@quantity", Quantity);
                    cmd.Parameters.AddWithValue("@id", Id);
                    cmd.ExecuteNonQuery();
                    cmd.Dispose();
                    return true;
                }
            }
        }

        // App-layer duplicate-barcode check before an insert (Inventory's
        // Add Product form) — belt-and-suspenders alongside the partial
        // UNIQUE index DatabaseBootstrapper.EnsureSchema() adds on
        // goods.Barcode (unique only when non-empty, so any number of
        // barcode-less products can coexist). Checking here first lets the
        // ViewModel show a clean, specific "that barcode is already used"
        // message instead of surfacing a raw SQLite constraint-violation
        // exception to the user.
        public bool BarcodeExists(string TableName, string Barcode)
        {
            string readString = "SELECT COUNT(*) FROM " + TableName + " WHERE Barcode = @barcode";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(readString, conn))
                {
                    cmd.Parameters.AddWithValue("@barcode", Barcode);
                    long count = (long)cmd.ExecuteScalar();
                    return count > 0;
                }
            }
        }

        // Everything below was added for Inventory's Edit Product /
        // category management features (2026-08-25).

        // Barcode-uniqueness check for editing an EXISTING product —
        // BarcodeExists above would always report "true" when a product's
        // own unchanged barcode is checked against itself, wrongly
        // blocking every edit of a product that already has a barcode.
        // Excludes the row being edited by ID, the same real primary key
        // UpdateGoodCountById already switched to and for the same reason
        // (see that method's comment) — Barcode itself is not reliably
        // unique once any barcode-less ("") product exists.
        public bool BarcodeExistsExcludingId(string TableName, string Barcode, int ExcludeId)
        {
            string readString = "SELECT COUNT(*) FROM " + TableName + " WHERE Barcode = @barcode AND ID <> @excludeid";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(readString, conn))
                {
                    cmd.Parameters.AddWithValue("@barcode", Barcode);
                    cmd.Parameters.AddWithValue("@excludeid", ExcludeId);
                    long count = (long)cmd.ExecuteScalar();
                    return count > 0;
                }
            }
        }

        // How many products currently sit in a given category — used to
        // block deleting a category still in use (InventoryViewModel.
        // DeleteCategory) rather than silently orphaning those products'
        // Category field to a name that no longer exists anywhere in the
        // picker.
        public int CountByCategory(string TableName, string Category)
        {
            string readString = "SELECT COUNT(*) FROM " + TableName + " WHERE Category = @category COLLATE NOCASE";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(readString, conn))
                {
                    cmd.Parameters.AddWithValue("@category", Category);
                    long count = (long)cmd.ExecuteScalar();
                    return (int)count;
                }
            }
        }

        // Added for Inventory's Delete Product feature (2026-08-25). The
        // legacy RemoveGoods above is Barcode-keyed, which has the exact
        // same bug already documented and fixed elsewhere in this class
        // (UpdateGoodCountById, BarcodeExistsExcludingId, UpdateGoodsById):
        // it would delete every barcode-less ("") product at once instead
        // of just the one intended, the moment more than one exists.
        // ID is the real, always-unique primary key.
        //
        // Safe with respect to sales history: Data/Sells.cs's `sells` table
        // (each sold line item) stores a full denormalized snapshot of the
        // product at sale time -- Name, Category, Cost, Price, Barcode are
        // all copied columns there, not a foreign key to goods.ID -- so
        // deleting a product here has no effect on any past sale's record.
        public bool RemoveGoodsById(string TableName, int Id)
        {
            string RemoveString = "DELETE FROM " + TableName + " WHERE ID = @id";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(RemoveString, conn))
                {
                    cmd.Parameters.AddWithValue("@id", Id);
                    cmd.ExecuteNonQuery();
                    cmd.Dispose();
                    return true;
                }
            }
        }

        // Full ID-keyed update for Inventory's Edit Product feature --
        // UpdateGoods (above, legacy) is Barcode-keyed and would silently
        // update every barcode-less product at once instead of the one
        // intended, the same problem UpdateGoodCountById was already added
        // to solve for quantity-only updates (see that method's comment).
        // Quantity is deliberately NOT a parameter here: the dedicated
        // Adjust-quantity flow (UpdateGoodCountById) already owns that
        // field with its own tested UI and event-raising, and conflating
        // the two risks a stale-buffer bug (Edit's own quantity snapshot
        // going out of date if an Adjust happens while an Edit is open) for
        // no real benefit — Type and Earned are left alone for the same
        // "don't touch a field this feature has no UI for" reasoning.
        // Widened 2026-09-04 to also accept DiscountPercent, for
        // Inventory's Discounts feature (bulk "Add Discounts" button + the
        // Discounts management page) -- both of those stage their change
        // through this same InventoryViewModel PushChange/Undo/Redo/Save
        // Changes pipeline everything else on this screen already goes
        // through (see that class's staging-model doc comment), rather
        // than writing to the database immediately on their own separate
        // path, so a pending discount change and a pending Name/Category/
        // Cost/Price/Barcode edit on the very same not-yet-saved row can
        // never race or partially commit against each other.
        public bool UpdateGoodsById(string TableName, int Id, string Name, string Category, double Cost, double Price, string Barcode, double DiscountPercent)
        {
            string UpdateString = "UPDATE " + TableName + " SET Name = @name, Category = @category, Cost = @cost, Price = @price, Barcode = @barcode, DiscountPercent = @discountpercent WHERE ID = @id";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(UpdateString, conn))
                {
                    cmd.Parameters.AddWithValue("@name", Name);
                    cmd.Parameters.AddWithValue("@category", Category);
                    cmd.Parameters.AddWithValue("@cost", Cost);
                    cmd.Parameters.AddWithValue("@price", Price);
                    cmd.Parameters.AddWithValue("@barcode", Barcode);
                    cmd.Parameters.AddWithValue("@discountpercent", DiscountPercent);
                    cmd.Parameters.AddWithValue("@id", Id);
                    cmd.ExecuteNonQuery();
                    cmd.Dispose();
                    return true;
                }
            }
        }

        // Added 2026-08-27 for item #6 (Bills view delete/reversal) --
        // restoring inventory when a sold line is deleted needs to find
        // WHICH goods row that line came from, but Data/Sells.cs's `sells`
        // table stores a denormalized snapshot per line (Name, Category,
        // Cost, Price, Barcode all copied at sale time, see RemoveGoodsById's
        // comment above) -- NOT a foreign key to goods.ID. So this is
        // necessarily a best-effort lookup, not a guaranteed match: prefer
        // Barcode (unique whenever non-empty, enforced by the partial UNIQUE
        // index DatabaseBootstrapper.EnsureSchema() adds), fall back to Name
        // when the line has no barcode. If the product was renamed, deleted,
        // or never had a distinguishing barcode since the original sale,
        // this can return null (nothing to restore -- caller must handle
        // that) or, in a Name collision between two different products,
        // the wrong row. Flagged, not solved -- solving it properly needs an
        // actual goods.ID column on `sells`, which is a schema change well
        // beyond this feature's scope.
        public Models.Goods FindGoodByBarcode(string TableName, string Barcode)
        {
            if (string.IsNullOrEmpty(Barcode)) return null;

            string readString = "SELECT * FROM " + TableName + " WHERE Barcode = @barcode LIMIT 1";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(readString, conn))
                {
                    cmd.Parameters.AddWithValue("@barcode", Barcode);
                    IDataReader reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        return new Models.Goods
                        {
                            Id = Convert.ToInt32(reader["ID"]),
                            Name = reader["Name"].ToString(),
                            Category = reader["Category"].ToString(),
                            Quantity = Convert.ToDouble(reader["Quantity"]),
                            Cost = Convert.ToDouble(reader["Cost"]),
                            Price = Convert.ToDouble(reader["Price"]),
                            Type = reader["Type"].ToString(),
                            Barcode = reader["Barcode"].ToString(),
                            Earned = Convert.ToDouble(reader["Earned"]),
                        };
                    }
                    return null;
                }
            }
        }

        public Models.Goods FindGoodByName(string TableName, string Name)
        {
            if (string.IsNullOrEmpty(Name)) return null;

            string readString = "SELECT * FROM " + TableName + " WHERE Name = @name LIMIT 1";
            using (SQLiteConnection conn = new SQLiteConnection(server.connectionString))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(readString, conn))
                {
                    cmd.Parameters.AddWithValue("@name", Name);
                    IDataReader reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        return new Models.Goods
                        {
                            Id = Convert.ToInt32(reader["ID"]),
                            Name = reader["Name"].ToString(),
                            Category = reader["Category"].ToString(),
                            Quantity = Convert.ToDouble(reader["Quantity"]),
                            Cost = Convert.ToDouble(reader["Cost"]),
                            Price = Convert.ToDouble(reader["Price"]),
                            Type = reader["Type"].ToString(),
                            Barcode = reader["Barcode"].ToString(),
                            Earned = Convert.ToDouble(reader["Earned"]),
                        };
                    }
                    return null;
                }
            }
        }
    }
}
