using System;
using System.Windows;

namespace PosSystem.App
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            RunCoreDataSmokeTest();
        }

        // Throwaway verification call — step 1 from the README's "Next steps".
        // Confirms PosSystem.Core actually reads rovaShop.db end to end:
        // SQLite connection -> Data layer -> Models -> visible on screen.
        //
        // Reads from `goods` (281 real rows in this seed db) rather than
        // `customers`/`bills`/`sells`, which are all empty in this particular
        // rovaShop.db — an empty-list result from those wouldn't tell us
        // whether reading actually works or whether the table is just empty.
        // Delete this whole method + the call above once a real Views/
        // screen exists.
        private void RunCoreDataSmokeTest()
        {
            try
            {
                var goodsData = new PosSystem.Core.Data.Goods();
                var goods = goodsData.ReadAllGoodsRPic("goods");

                if (goods.Count == 0)
                {
                    StatusText.Text = "Connected to rovaShop.db, but 'goods' returned 0 rows.";
                    return;
                }

                var first = goods[0];
                StatusText.Text =
                    $"Core -> SQLite OK: {goods.Count} rows in 'goods'." +
                    Environment.NewLine +
                    $"First item: \"{first.Name}\" — Price {first.Price:0.00}, Qty {first.Quantity:0.##}";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Core data read FAILED: " + ex.Message;
            }
        }
    }
}
