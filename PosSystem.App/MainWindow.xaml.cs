using System;
using System.Windows;
using PosSystem.App.Theming;

namespace PosSystem.App
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            RunCoreDataSmokeTest();
        }

        // Throwaway proof that Colors.Light.xaml / Colors.Dark.xaml swap
        // cleanly at runtime. Move this into a real Settings screen once one
        // exists; ThemeManager.Toggle() itself is not throwaway.
        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.Toggle();
        }

        // Throwaway verification call — step 1 from the README's "Next steps".
        // Confirms PosSystem.Core actually reads rovaShop.db end to end:
        // SQLite connection -> Data layer -> Models -> visible on screen.
        //
        // Reads `goods` (281 real rows) AND `customers` (now seeded with 8
        // test rows covering fully-paid, partial-debt, and all-debt states —
        // see README for details). The `customers` table was empty in the
        // original seed db, so this couldn't be verified until now.
        // Delete this whole method + the call above once real Views/ screens
        // exist.
        private void RunCoreDataSmokeTest()
        {
            try
            {
                var goodsData = new PosSystem.Core.Data.Goods();
                var goods = goodsData.ReadAllGoodsRPic("goods");

                var customersData = new PosSystem.Core.Data.Customers();
                var customers = customersData.ReadCustomers("customers");

                if (goods.Count == 0 || customers.Count == 0)
                {
                    StatusText.Text =
                        $"Connected to rovaShop.db, but got {goods.Count} goods / {customers.Count} customers.";
                    return;
                }

                var firstGood = goods[0];
                double totalOwed = 0;
                foreach (var c in customers) totalOwed += c.Remain;

                StatusText.Text =
                    $"Core -> SQLite OK: {goods.Count} goods, {customers.Count} customers." +
                    Environment.NewLine +
                    $"First item: \"{firstGood.Name}\" — Price {firstGood.Price:0.00}" +
                    Environment.NewLine +
                    $"Total customer debt outstanding: {totalOwed:0.00}";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Core data read FAILED: " + ex.Message;
            }
        }
    }
}
