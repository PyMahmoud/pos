using System;

namespace PosSystem.App.ViewModels
{
    /// <summary>
    /// DataContext for DashboardView. For now this just runs the same
    /// Core -> SQLite smoke test that used to live directly in MainWindow —
    /// moved here because Dashboard is the natural home for
    /// at-a-glance data, and it means the nav shell has real (if minimal)
    /// content on day one instead of four empty placeholder screens.
    ///
    /// Replace StatusText and this whole verification block with the real
    /// dashboard (sold items + monthly revenue charts, per the business plan)
    /// when that step comes up.
    /// </summary>
    public class DashboardViewModel : ViewModelBase
    {
        private string _statusText = "Loading...";
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public DashboardViewModel()
        {
            RunCoreDataSmokeTest();
        }

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
                    StatusText =
                        $"Connected to rovaShop.db, but got {goods.Count} goods / {customers.Count} customers.";
                    return;
                }

                var firstGood = goods[0];
                double totalOwed = 0;
                foreach (var c in customers) totalOwed += c.Remain;

                StatusText =
                    $"Core -> SQLite OK: {goods.Count} goods, {customers.Count} customers." +
                    Environment.NewLine +
                    $"First item: \"{firstGood.Name}\" — Price {firstGood.Price:0.00}" +
                    Environment.NewLine +
                    $"Total customer debt outstanding: {totalOwed:0.00}";
            }
            catch (Exception ex)
            {
                StatusText = "Core data read FAILED: " + ex.Message;
            }
        }
    }
}
