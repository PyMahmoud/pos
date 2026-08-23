using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using PosSystem.App.Localization;
using PosSystem.App.Theming;

namespace PosSystem.App.ViewModels
{
    /// <summary>
    /// DataContext for DashboardView. Phase 6: real KPIs (today's revenue,
    /// today's profit, today's transaction count, outstanding customer
    /// debt) and two charts (top-selling items all-time, today's
    /// Cash/Card/Pay-Later split) — built off Core.Data directly, no new
    /// SQL added, same approach Checkout and Customers already took.
    ///
    /// Refresh is event-driven, not polled, per Phase 6's explicit
    /// requirement (near-zero CPU usage between sales on old hardware):
    /// - OrderEvents.OrderCompleted fires after every completed Checkout sale
    /// - LocalizationManager.LanguageChanged re-localizes the payment-split
    ///   legend labels ("Cash"/"Card"/"Pay Later")
    /// - ThemeManager.ThemeChanged re-reads the active theme's brush colors
    ///   for the chart series. LiveCharts series colors are plain SkiaSharp
    ///   colors set in code, not WPF resources — they will NOT repaint on
    ///   their own just because Colors.Light/Dark.xaml got swapped, the way
    ///   a DynamicResource brush would. Without this subscription, toggling
    ///   theme would leave stale-colored charts until the next sale.
    /// DashboardView (like every screen) is created once and cached by
    /// MainViewModel, so these three subscriptions live for the app's
    /// lifetime — there's no matching Unsubscribe anywhere else in the app
    /// either, so this isn't a new pattern.
    ///
    /// NOTE: this file uses the LiveChartsCore.SkiaSharpView.WPF package,
    /// which has NOT been installed via NuGet as of writing — I can't fetch
    /// packages myself (no nuget.org access, no Windows/MSBuild to restore
    /// against). Install "LiveChartsCore.SkiaSharpView.WPF" via the NuGet
    /// Package Manager before building. If a build error here is a missing
    /// type/member rather than a missing package, the exact API surface
    /// (ISeries, ColumnSeries&lt;T&gt;, PieSeries&lt;T&gt;, Axis,
    /// SolidColorPaint) may have shifted between package versions — flag it
    /// and we'll fix it against whatever version actually installs.
    /// </summary>
    public class DashboardViewModel : ViewModelBase
    {
        private readonly Core.Data.Bills _billsData = new Core.Data.Bills();
        private readonly Core.Data.Sells _sellsData = new Core.Data.Sells();
        private readonly Core.Data.Customers _customersData = new Core.Data.Customers();

        private double _todayRevenue;
        public double TodayRevenue
        {
            get => _todayRevenue;
            set => SetProperty(ref _todayRevenue, value);
        }

        private double _todayProfit;
        public double TodayProfit
        {
            get => _todayProfit;
            set => SetProperty(ref _todayProfit, value);
        }

        private int _todayTransactionCount;
        public int TodayTransactionCount
        {
            get => _todayTransactionCount;
            set => SetProperty(ref _todayTransactionCount, value);
        }

        private double _outstandingDebtTotal;
        public double OutstandingDebtTotal
        {
            get => _outstandingDebtTotal;
            set => SetProperty(ref _outstandingDebtTotal, value);
        }

        private bool _hasAnyBillsEver;
        public bool HasAnyBillsEver
        {
            get => _hasAnyBillsEver;
            set
            {
                if (SetProperty(ref _hasAnyBillsEver, value)) OnPropertyChanged(nameof(NoDataYet));
            }
        }

        // Inverse of HasAnyBillsEver, purely so the XAML can bind a plain
        // BoolToVisibilityConverter (true = Visible) for the "no sales yet"
        // message instead of needing a second, negated converter.
        public bool NoDataYet => !HasAnyBillsEver;

        private string _lastUpdatedText = "";
        public string LastUpdatedText
        {
            get => _lastUpdatedText;
            set => SetProperty(ref _lastUpdatedText, value);
        }

        public ObservableCollection<ISeries> TopItemsSeries { get; } = new ObservableCollection<ISeries>();

        private Axis[] _topItemsXAxes = { new Axis() };
        public Axis[] TopItemsXAxes
        {
            get => _topItemsXAxes;
            private set => SetProperty(ref _topItemsXAxes, value);
        }

        private Axis[] _topItemsYAxes = { new Axis() };
        public Axis[] TopItemsYAxes
        {
            get => _topItemsYAxes;
            private set => SetProperty(ref _topItemsYAxes, value);
        }

        public ObservableCollection<ISeries> PaymentSplitSeries { get; } = new ObservableCollection<ISeries>();

        public DashboardViewModel()
        {
            OrderEvents.OrderCompleted += RefreshDashboard;
            ThemeManager.ThemeChanged += _ => RefreshDashboard();
            LocalizationManager.LanguageChanged += _ => RefreshDashboard();

            RefreshDashboard();
        }

        private void RefreshDashboard()
        {
            // Event handlers above could in principle fire from a
            // non-UI thread; ObservableCollection isn't thread-safe to
            // mutate off the dispatcher, so hop back on it if needed.
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(RefreshDashboard);
                return;
            }

            try
            {
                string today = DateTime.Now.ToString("dd/MM/yyyy");

                var allBills = _billsData.ReadBills("bills");
                var todayBills = allBills.Where(b => b.Datex == today).ToList();

                TodayRevenue = todayBills.Sum(b => b.Billcost);
                TodayProfit = todayBills.Sum(b => b.Earned);
                TodayTransactionCount = todayBills.Count;
                HasAnyBillsEver = allBills.Count > 0;

                var customers = _customersData.ReadCustomers("customers");
                OutstandingDebtTotal = customers.Sum(c => c.Remain);

                BuildTopItemsChart();
                BuildPaymentSplitChart(todayBills);

                LastUpdatedText = DateTime.Now.ToString("HH:mm:ss");
            }
            catch (Exception ex)
            {
                LastUpdatedText = "Error: " + ex.Message;
            }
        }

        private void BuildTopItemsChart()
        {
            // All-time, not today-only — Checkout's own test sales plus
            // whatever's in the seed data give this chart something
            // meaningful to show even on a quiet day. Today's Revenue above
            // is the one figure that's genuinely date-filtered.
            var allSells = _sellsData.ReadPendingSell("sells");

            var topItems = allSells
                .GroupBy(s => s.Name)
                .Select(g => new { Name = g.Key, Quantity = g.Sum(s => s.Quantity) })
                .OrderByDescending(x => x.Quantity)
                .Take(5)
                .ToList();

            var primary = GetThemeColor("PrimaryColor", "#6C4CE0");

            TopItemsSeries.Clear();
            TopItemsSeries.Add(new ColumnSeries<double>
            {
                Values = topItems.Select(x => x.Quantity).ToArray(),
                Name = "Units sold",
                Fill = new SolidColorPaint(primary),
                MaxBarWidth = 42
            });

            TopItemsXAxes = new[] { new Axis { Labels = topItems.Select(x => x.Name).ToArray() } };
            TopItemsYAxes = new[] { new Axis { MinLimit = 0 } };
        }

        private void BuildPaymentSplitChart(List<Core.Models.Bills> todayBills)
        {
            double cashTotal = todayBills.Where(b => b.Details == "Cash").Sum(b => b.Billcost);
            double cardTotal = todayBills.Where(b => b.Details == "Card").Sum(b => b.Billcost);
            double creditTotal = todayBills.Where(b => b.Details == "Credit").Sum(b => b.Billcost);

            var primary = GetThemeColor("PrimaryColor", "#6C4CE0");
            var secondary = GetThemeColor("SecondaryColor", "#615594");
            var tertiary = GetThemeColor("TertiaryColor", "#7C3F00");

            string cashLabel = LocalizationManager.GetString("CheckoutCash");
            string cardLabel = LocalizationManager.GetString("CheckoutCard");
            string creditLabel = LocalizationManager.GetString("CheckoutPayLater");

            PaymentSplitSeries.Clear();

            if (cashTotal > 0)
                PaymentSplitSeries.Add(new PieSeries<double>
                {
                    Values = new[] { cashTotal },
                    Name = cashLabel,
                    Fill = new SolidColorPaint(primary)
                });

            if (cardTotal > 0)
                PaymentSplitSeries.Add(new PieSeries<double>
                {
                    Values = new[] { cardTotal },
                    Name = cardLabel,
                    Fill = new SolidColorPaint(secondary)
                });

            if (creditTotal > 0)
                PaymentSplitSeries.Add(new PieSeries<double>
                {
                    Values = new[] { creditTotal },
                    Name = creditLabel,
                    Fill = new SolidColorPaint(tertiary)
                });
        }

        // Reads straight from the currently-active theme's Color resources
        // (set by Colors.Light.xaml / Colors.Dark.xaml — see
        // Theming/ThemeManager.cs) rather than hardcoding hex per theme, so
        // chart colors always match whichever palette is actually active.
        private static SKColor GetThemeColor(string resourceKey, string fallbackHex)
        {
            try
            {
                if (Application.Current?.Resources[resourceKey] is Color c)
                    return new SKColor(c.R, c.G, c.B, c.A);
            }
            catch
            {
                // Fall through to the fallback below.
            }
            return SKColor.Parse(fallbackHex);
        }
    }
}
