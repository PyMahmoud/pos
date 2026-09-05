using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using LiveChartsCore;
using LiveChartsCore.Kernel;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using PosSystem.App.Localization;
using PosSystem.App.Theming;

namespace PosSystem.App.ViewModels
{
    /// <summary>
    /// Which quick-range chip (if any) is active. Custom means the staff
    /// member is using the two date pickers directly rather than a preset —
    /// set automatically the moment either date picker is edited, so the
    /// highlighted chip always reflects what's actually being shown, never
    /// a stale one.
    /// </summary>
    public enum DashboardQuickRange
    {
        Today,
        ThisWeek,
        ThisMonth,
        Last30Days,
        AllTime,
        Custom
    }

    /// <summary>
    /// DataContext for DashboardView.
    ///
    /// Dashboard-Parity-Plan.md Stages 2–4 are implemented here, but not
    /// as literal ports of the Power BI reference — see the design notes
    /// on each piece below for what changed and why. Short version: every
    /// interaction is an ordinary WPF button/chip/date-picker, nothing
    /// depends on clicking a chart element. That's a deliberate call per
    /// the plan's own Usability rules (real touch targets, no
    /// icon-only/imprecise affordances) over pixel-matching the reference,
    /// and it means none of this depends on a LiveCharts click-event API
    /// I have no way to verify without a compiler — unlike everything
    /// else in this file, which is plain LINQ over already-known model
    /// classes and carries the same confidence level as the rest of the app.
    ///
    /// Filtering pipeline: FilterStartDate/FilterEndDate (date range),
    /// SelectedPaymentChip (Cash/Card/Pay Later/All), and
    /// SelectedCategoryChip (a real category from the data, or All) all
    /// combine into one filtered set of `sells` rows per recompute — KPIs,
    /// Top Items, the payment-split donut, and the category-revenue chart
    /// are ALL derived from that same filtered set, so they always agree
    /// with each other no matter which filters are combined (this is what
    /// makes it "cross-filtering" rather than independently-filtered charts
    /// that could disagree). The revenue trend line uses the same filtered
    /// set too, bucketed by day (or by week if the selected range is long —
    /// Performance rule: cap chart points on old hardware).
    ///
    /// Data is loaded from SQLite once per RefreshDashboard (real change —
    /// a completed sale, theme, or language) and cached in _cachedBills/
    /// _cachedSells; every filter-only interaction (date pickers, chips)
    /// calls the lighter RecomputeAndRedraw instead, which re-filters the
    /// cached lists in memory with zero database round-trips — the
    /// Performance rule this plan is built around, applied for real rather
    /// than just stated.
    ///
    /// "Drill-down" (Stage 4) is the Today quick-range chip, not a
    /// click-a-point-on-the-line-chart interaction. Tapping Today collapses
    /// every KPI/chart on the screen down to a single day's detail; tapping
    /// Last 30 Days zooms back out. Same practical value as the reference's
    /// month→day drill (progressively more detail on demand), a much
    /// bigger and more reliable touch target, and no new fragile chart-click
    /// code — a direct call under Usability rule 6 (simplify rather than
    /// force a literal port when it doesn't fit).
    ///
    /// Refresh is event-driven, not polled (Phase 6's explicit requirement):
    /// - OrderEvents.OrderCompleted fires after every completed Checkout sale
    /// - LocalizationManager.LanguageChanged re-localizes chip labels and
    ///   chart titles, and rebuilds the category chip list (same reason
    ///   CheckoutViewModel.RebuildCategoryChips exists)
    /// - ThemeManager.ThemeChanged re-reads the active theme's brush colors
    ///   for the chart series, which are plain SkiaSharp colors set in code
    ///   and won't repaint on their own from a DynamicResource swap
    /// DashboardView is created once and cached by MainViewModel, so these
    /// subscriptions live for the app's lifetime — consistent with every
    /// other screen; nothing here unsubscribes and nothing else in the app
    /// does either.
    ///
    /// NOTE, unchanged from Phase 6: this file uses
    /// LiveChartsCore.SkiaSharpView.WPF, which I can't install or restore
    /// myself (no Windows/MSBuild access from here). If a build error here
    /// is a missing type/member rather than a missing package, the exact
    /// API surface may have shifted between package versions — flag it and
    /// we'll fix it against whatever version is actually installed.
    /// </summary>
    public class DashboardViewModel : ViewModelBase
    {
        // Admin gate (#7, 2026-08-27; moved onto the shared AdminSession
        // 2026-08-27 round 2) — revenue is business-sensitive, so the
        // business owner/manager decided only someone who knows the admin
        // password should see this screen's numbers. Unlocking here (or on
        // Inventory, or the eventual Excel export) now unlocks all of them
        // for the rest of the app session — see AdminSession's class doc
        // comment for why this moved off a per-ViewModel private field. If
        // no admin password has ever been set (AppSettings.HasAdminPassword
        // is false), AdminSession starts unlocked, so the Dashboard stays
        // open exactly as it always has on a fresh install.
        // Admin gate (#7, 2026-08-27) -- reworked (per Mahmoud's explicit
        // request) so this screen's unlock is independent and temporary:
        // unlocking Dashboard does NOT unlock Inventory, Bills, or any of
        // Settings' gated sections, and leaving this screen (switching
        // sidebar tabs) re-locks it -- the password has to be re-entered
        // next time Dashboard is opened, even though this ViewModel
        // instance itself is cached for the app's lifetime like every
        // other screen (MainViewModel's view cache). _isUnlockedThisVisit
        // is a private, per-ViewModel flag -- not shared via any static
        // session class. LockAdmin() below resets it and is called from
        // DashboardView's Unloaded event, which fires whenever this cached
        // view leaves the visual tree (i.e. the sidebar selection moves
        // elsewhere), even though the instance survives. If no admin
        // password has ever been set (AppSettings.HasAdminPassword is
        // false), this stays unlocked unconditionally, same as before.
        private bool _isUnlockedThisVisit;
        // GateDashboardEnabled (Settings' new Access Control section, added
        // per Mahmoud's request) -- lets this screen stay open even with a
        // password set elsewhere, if the owner turns Dashboard's own switch
        // off. Same short-circuit shape as HasAdminPassword's existing
        // check, just a second independent reason to skip the prompt.
        public bool IsUnlocked => !AppSettings.HasAdminPassword || !AppSettings.GateDashboardEnabled || _isUnlockedThisVisit;
        public bool IsLocked => !IsUnlocked;

        private string _unlockPasswordInput = "";
        public string UnlockPasswordInput
        {
            get => _unlockPasswordInput;
            set => SetProperty(ref _unlockPasswordInput, value);
        }

        private string _unlockError = "";
        public string UnlockError
        {
            get => _unlockError;
            set => SetProperty(ref _unlockError, value);
        }

        public ICommand UnlockCommand { get; }

        private void Unlock()
        {
            if (AppSettings.VerifyAdminPassword(UnlockPasswordInput))
            {
                _isUnlockedThisVisit = true;
                OnPropertyChanged(nameof(IsUnlocked));
                OnPropertyChanged(nameof(IsLocked));
                UnlockError = "";
                UnlockPasswordInput = "";
            }
            else
            {
                UnlockError = LocalizationManager.GetString("DashboardUnlockIncorrect");
            }
        }

        /// <summary>
        /// Re-locks this screen -- called from DashboardView's Unloaded
        /// event when the sidebar selection moves away from Dashboard, so
        /// coming back later requires the password again instead of
        /// staying unlocked for the rest of the app session.
        /// </summary>
        public void LockAdmin()
        {
            if (!_isUnlockedThisVisit) return;
            _isUnlockedThisVisit = false;
            UnlockPasswordInput = "";
            UnlockError = "";
            OnPropertyChanged(nameof(IsUnlocked));
            OnPropertyChanged(nameof(IsLocked));
        }

        private readonly Core.Data.Bills _billsData = new Core.Data.Bills();
        private readonly Core.Data.Sells _sellsData = new Core.Data.Sells();
        private readonly Core.Data.Customers _customersData = new Core.Data.Customers();

        // Set true while ApplyQuickRange is setting both dates at once, so
        // the FilterStartDate/FilterEndDate setters below don't stomp
        // ActiveRange back to Custom while a chip's own handler is in the
        // middle of setting both dates to match it.
        private bool _isApplyingQuickRange;

        // Cached from the last real database read (RefreshDashboard).
        // Filter-only changes reuse these via RecomputeAndRedraw instead of
        // re-querying SQLite — see the class-level comment above.
        private List<Core.Models.Bills> _cachedBills = new List<Core.Models.Bills>();
        private List<Core.Models.Sells> _cachedSells = new List<Core.Models.Sells>();

        // ----- KPIs (all four reflect the active filter, EXCEPT
        //       OutstandingDebtTotal — a live balance snapshot, not a
        //       period metric, so filtering it by date wouldn't mean
        //       anything real; it stays as "right now, unfiltered") -----

        private double _filteredRevenue;
        public double FilteredRevenue
        {
            get => _filteredRevenue;
            set => SetProperty(ref _filteredRevenue, value);
        }

        private double _filteredProfit;
        public double FilteredProfit
        {
            get => _filteredProfit;
            set => SetProperty(ref _filteredProfit, value);
        }

        private int _filteredTransactionCount;
        public int FilteredTransactionCount
        {
            get => _filteredTransactionCount;
            set => SetProperty(ref _filteredTransactionCount, value);
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

        // Human-readable summary of the active filter range, shown under the
        // heading so a number is never on screen without saying what period
        // it covers (Usability rule: no unlabeled numbers).
        private string _activeRangeText = "";
        public string ActiveRangeText
        {
            get => _activeRangeText;
            set => SetProperty(ref _activeRangeText, value);
        }

        // ----- Date range (Stage 2) -----

        private DateTime? _filterStartDate;
        public DateTime? FilterStartDate
        {
            get => _filterStartDate;
            set
            {
                if (!SetProperty(ref _filterStartDate, value)) return;
                if (!_isApplyingQuickRange) ActiveRange = DashboardQuickRange.Custom;
                RecomputeAndRedraw();
            }
        }

        private DateTime? _filterEndDate;
        public DateTime? FilterEndDate
        {
            get => _filterEndDate;
            set
            {
                if (!SetProperty(ref _filterEndDate, value)) return;
                if (!_isApplyingQuickRange) ActiveRange = DashboardQuickRange.Custom;
                RecomputeAndRedraw();
            }
        }

        private DateTime EffectiveStart => (FilterStartDate ?? DateTime.Today.AddDays(-29)).Date;
        private DateTime EffectiveEnd => (FilterEndDate ?? DateTime.Today).Date;

        private DashboardQuickRange _activeRange = DashboardQuickRange.Last30Days;
        public DashboardQuickRange ActiveRange
        {
            get => _activeRange;
            private set
            {
                if (!SetProperty(ref _activeRange, value)) return;
                OnPropertyChanged(nameof(IsRangeTodaySelected));
                OnPropertyChanged(nameof(IsRangeWeekSelected));
                OnPropertyChanged(nameof(IsRangeMonthSelected));
                OnPropertyChanged(nameof(IsRangeLast30Selected));
                OnPropertyChanged(nameof(IsRangeAllTimeSelected));
            }
        }

        // Same IsXSelected-bool-per-option pattern CheckoutViewModel already
        // uses for its Cash/Card/Pay Later buttons — reused here rather
        // than inventing a converter, per the design-system rule against
        // parallel patterns for the same kind of thing.
        public bool IsRangeTodaySelected => ActiveRange == DashboardQuickRange.Today;
        public bool IsRangeWeekSelected => ActiveRange == DashboardQuickRange.ThisWeek;
        public bool IsRangeMonthSelected => ActiveRange == DashboardQuickRange.ThisMonth;
        public bool IsRangeLast30Selected => ActiveRange == DashboardQuickRange.Last30Days;
        public bool IsRangeAllTimeSelected => ActiveRange == DashboardQuickRange.AllTime;

        // ----- Cross-filters (Stage 3): payment method + category -----

        public ObservableCollection<CategoryChip> PaymentChips { get; } = new ObservableCollection<CategoryChip>();

        private CategoryChip _selectedPaymentChip;
        public CategoryChip SelectedPaymentChip
        {
            get => _selectedPaymentChip;
            set
            {
                if (!SetProperty(ref _selectedPaymentChip, value)) return;
                OnPropertyChanged(nameof(HasActiveCrossFilter));
                RecomputeAndRedraw();
            }
        }

        public ObservableCollection<CategoryChip> CategoryChips { get; } = new ObservableCollection<CategoryChip>();

        // Type-to-search category dropdown (2026-08-26) — same design as
        // CheckoutViewModel's own FilteredCategoryOptions/CategorySearchText
        // (see that class's doc comment for the full reasoning), ported here
        // for Dashboard's cross-filter category chip. CategoryChips above
        // stays the full master list, still rebuilt by RebuildCategoryChips
        // from the real (filtered-set-independent) category data each
        // RefreshDashboard; FilteredCategoryChipOptions is what the ComboBox
        // actually shows, narrowed by CategoryChipSearchText as the user
        // types.
        public ObservableCollection<CategoryChip> FilteredCategoryChipOptions { get; } = new ObservableCollection<CategoryChip>();

        private bool _suppressCategoryChipFilter;

        private string _categoryChipSearchText = "";
        public string CategoryChipSearchText
        {
            get => _categoryChipSearchText;
            set
            {
                if (!SetProperty(ref _categoryChipSearchText, value)) return;
                if (_suppressCategoryChipFilter) return;
                RebuildFilteredCategoryChips();
                IsCategoryChipDropDownOpen = true;
            }
        }

        private bool _isCategoryChipDropDownOpen;
        public bool IsCategoryChipDropDownOpen
        {
            get => _isCategoryChipDropDownOpen;
            set => SetProperty(ref _isCategoryChipDropDownOpen, value);
        }

        private CategoryChip _selectedCategoryChip;
        public CategoryChip SelectedCategoryChip
        {
            get => _selectedCategoryChip;
            set
            {
                if (!SetProperty(ref _selectedCategoryChip, value)) return;
                OnPropertyChanged(nameof(HasActiveCrossFilter));
                RecomputeAndRedraw();

                _suppressCategoryChipFilter = true;
                CategoryChipSearchText = value?.DisplayName ?? "";
                _suppressCategoryChipFilter = false;
            }
        }

        public bool HasActiveCrossFilter =>
            (SelectedPaymentChip?.Value != null) || (SelectedCategoryChip?.Value != null);

        public ICommand SetQuickRangeCommand { get; }
        public ICommand ClearFiltersCommand { get; }

        // ----- Charts -----

        public ObservableCollection<ISeries> TopItemsSeries { get; } = new ObservableCollection<ISeries>();
        private Axis[] _topItemsXAxes = { new Axis() };
        public Axis[] TopItemsXAxes { get => _topItemsXAxes; private set => SetProperty(ref _topItemsXAxes, value); }
        private Axis[] _topItemsYAxes = { new Axis() };
        public Axis[] TopItemsYAxes { get => _topItemsYAxes; private set => SetProperty(ref _topItemsYAxes, value); }

        public ObservableCollection<ISeries> PaymentSplitSeries { get; } = new ObservableCollection<ISeries>();

        // Legend text color for PaymentSplitSeries (Payment Split now shows
        // a legend since it's a full circle, not a labeled donut). Like the
        // series Fill colors above, this is a plain SkiaSharp paint set in
        // code, so it won't repaint on its own when the theme swaps a
        // DynamicResource brush — UpdateChartLegendTextPaint() (called from
        // RefreshDashboard, which already runs on ThemeManager.ThemeChanged)
        // keeps it in sync instead.
        private SolidColorPaint _chartLegendTextPaint;
        public SolidColorPaint ChartLegendTextPaint
        {
            get => _chartLegendTextPaint;
            private set => SetProperty(ref _chartLegendTextPaint, value);
        }

        // Tooltip colors (the popup shown when hovering a chart point/bar) —
        // same category of bug as the axis colors above: LiveChartsCore
        // defaults these to its own light-theme colors regardless of the
        // app's active theme, which is why the tooltip in the reported
        // screenshot was a light box floating on a dark chart. Set from
        // RefreshDashboard alongside ChartLegendTextPaint, same trigger set
        // (real data reload, theme toggle, language toggle).
        private SolidColorPaint _chartTooltipBackgroundPaint;
        public SolidColorPaint ChartTooltipBackgroundPaint
        {
            get => _chartTooltipBackgroundPaint;
            private set => SetProperty(ref _chartTooltipBackgroundPaint, value);
        }

        private SolidColorPaint _chartTooltipTextPaint;
        public SolidColorPaint ChartTooltipTextPaint
        {
            get => _chartTooltipTextPaint;
            private set => SetProperty(ref _chartTooltipTextPaint, value);
        }

        public ObservableCollection<ISeries> RevenueTrendSeries { get; } = new ObservableCollection<ISeries>();
        private Axis[] _revenueTrendXAxes = { new Axis() };
        public Axis[] RevenueTrendXAxes { get => _revenueTrendXAxes; private set => SetProperty(ref _revenueTrendXAxes, value); }
        private Axis[] _revenueTrendYAxes = { new Axis() };
        public Axis[] RevenueTrendYAxes { get => _revenueTrendYAxes; private set => SetProperty(ref _revenueTrendYAxes, value); }

        // New in Stage 3 — matches the reference's "Revenue by Category" panel.
        public ObservableCollection<ISeries> CategoryRevenueSeries { get; } = new ObservableCollection<ISeries>();
        private Axis[] _categoryRevenueXAxes = { new Axis() };
        public Axis[] CategoryRevenueXAxes { get => _categoryRevenueXAxes; private set => SetProperty(ref _categoryRevenueXAxes, value); }
        private Axis[] _categoryRevenueYAxes = { new Axis() };
        public Axis[] CategoryRevenueYAxes { get => _categoryRevenueYAxes; private set => SetProperty(ref _categoryRevenueYAxes, value); }

        public DashboardViewModel()
        {
            UnlockCommand = new RelayCommand(_ => Unlock());
            AppSettings.Changed += () =>
            {
                OnPropertyChanged(nameof(IsUnlocked));
                OnPropertyChanged(nameof(IsLocked));
            };
            SetQuickRangeCommand = new RelayCommand(p =>
            {
                if (p is DashboardQuickRange range) ApplyQuickRange(range);
            });
            ClearFiltersCommand = new RelayCommand(_ =>
            {
                SelectedPaymentChip = PaymentChips.FirstOrDefault(c => c.Value == null) ?? SelectedPaymentChip;
                SelectedCategoryChip = CategoryChips.FirstOrDefault(c => c.Value == null) ?? SelectedCategoryChip;
                ApplyQuickRange(DashboardQuickRange.Last30Days);
            });

            OrderEvents.OrderCompleted += RefreshDashboard;
            ThemeManager.ThemeChanged += _ => RefreshDashboard();
            LocalizationManager.LanguageChanged += _ =>
            {
                RebuildPaymentChips();
                RefreshDashboard(); // also rebuilds category chips (real data, not just labels)
            };

            RebuildPaymentChips();
            // Order matters: ApplyQuickRange sets FilterStartDate/EndDate and
            // ActiveRange, which via the property setters above triggers
            // RecomputeAndRedraw — but against still-empty _cachedBills/
            // _cachedSells at this point, since no real data load has
            // happened yet. RefreshDashboard right after does that load and
            // redraws for real; the harmless empty recompute just before it
            // is the cost of reusing ApplyQuickRange's date-computation logic
            // here instead of duplicating it for "first load" specifically.
            ApplyQuickRange(DashboardQuickRange.Last30Days);
            RefreshDashboard();
        }

        private void ApplyQuickRange(DashboardQuickRange range)
        {
            _isApplyingQuickRange = true;
            try
            {
                DateTime today = DateTime.Today;
                switch (range)
                {
                    case DashboardQuickRange.Today:
                        FilterStartDate = today;
                        FilterEndDate = today;
                        break;
                    case DashboardQuickRange.ThisWeek:
                        // Week starts Saturday to match the region this app
                        // was built for (Egypt) rather than assuming Monday.
                        int daysSinceSaturday = ((int)today.DayOfWeek + 1) % 7;
                        FilterStartDate = today.AddDays(-daysSinceSaturday);
                        FilterEndDate = today;
                        break;
                    case DashboardQuickRange.ThisMonth:
                        FilterStartDate = new DateTime(today.Year, today.Month, 1);
                        FilterEndDate = today;
                        break;
                    case DashboardQuickRange.Last30Days:
                        FilterStartDate = today.AddDays(-29);
                        FilterEndDate = today;
                        break;
                    case DashboardQuickRange.AllTime:
                        FilterStartDate = GetEarliestDataDate();
                        FilterEndDate = today;
                        break;
                    default:
                        FilterStartDate = today.AddDays(-29);
                        FilterEndDate = today;
                        break;
                }
                ActiveRange = range;
            }
            finally
            {
                _isApplyingQuickRange = false;
            }
        }

        /// <summary>
        /// Earliest sale date on record, for the All Time quick range.
        /// Reads from _cachedSells (already-loaded data, same rule as
        /// RecomputeAndRedraw — never a fresh SQLite query just to answer
        /// this). If All Time is picked before any real data has loaded
        /// (e.g. theoretically on first construction, though the default
        /// quick range is Last30Days, not AllTime, so this shouldn't
        /// actually happen in practice) or the shop has no sales yet, falls
        /// back to 5 years ago rather than leaving the range empty/invalid.
        /// </summary>
        private DateTime GetEarliestDataDate()
        {
            DateTime? earliest = null;
            foreach (var sell in _cachedSells)
            {
                if (TryParseDatex(sell.Datex, out DateTime d) && (earliest == null || d < earliest))
                    earliest = d;
            }
            return (earliest ?? DateTime.Today.AddYears(-5)).Date;
        }

        private void RebuildPaymentChips()
        {
            string previousValue = SelectedPaymentChip?.Value;

            PaymentChips.Clear();
            PaymentChips.Add(new CategoryChip { DisplayName = LocalizationManager.GetString("CheckoutAllCategory"), Value = null });
            PaymentChips.Add(new CategoryChip { DisplayName = LocalizationManager.GetString("CheckoutCash"), Value = "Cash" });
            PaymentChips.Add(new CategoryChip { DisplayName = LocalizationManager.GetString("CheckoutCard"), Value = "Card" });
            PaymentChips.Add(new CategoryChip { DisplayName = LocalizationManager.GetString("CheckoutPayLater"), Value = "Credit" });

            _selectedPaymentChip = PaymentChips.FirstOrDefault(c => c.Value == previousValue) ?? PaymentChips[0];
            OnPropertyChanged(nameof(SelectedPaymentChip));
        }

        private void RebuildCategoryChips(List<Core.Models.Sells> allSells)
        {
            string previousValue = SelectedCategoryChip?.Value;

            CategoryChips.Clear();
            CategoryChips.Add(new CategoryChip { DisplayName = LocalizationManager.GetString("CheckoutAllCategory"), Value = null });
            foreach (var category in allSells
                         .Select(s => s.Category)
                         .Where(c => !string.IsNullOrWhiteSpace(c))
                         .Distinct()
                         .OrderBy(c => c))
            {
                CategoryChips.Add(new CategoryChip { DisplayName = category, Value = category });
            }

            _selectedCategoryChip = CategoryChips.FirstOrDefault(c => c.Value == previousValue) ?? CategoryChips[0];
            OnPropertyChanged(nameof(SelectedCategoryChip));

            // See CheckoutViewModel.RebuildCategoryChips' matching comment —
            // this bypasses the SelectedCategoryChip setter (direct field
            // assignment above), so the search-box sync has to happen here
            // too.
            _suppressCategoryChipFilter = true;
            CategoryChipSearchText = _selectedCategoryChip?.DisplayName ?? "";
            _suppressCategoryChipFilter = false;
            RebuildFilteredCategoryChips();
        }

        // See CheckoutViewModel.RebuildFilteredCategories for the full
        // reasoning (same design, ported here for Dashboard's category
        // cross-filter chip). "All" (CategoryChips[0], Value == null) is
        // always kept reachable even when the typed text doesn't match its
        // own label.
        private void RebuildFilteredCategoryChips()
        {
            FilteredCategoryChipOptions.Clear();
            string text = CategoryChipSearchText ?? "";
            CategoryChip allChip = CategoryChips.Count > 0 ? CategoryChips[0] : null;

            if (string.IsNullOrWhiteSpace(text))
            {
                foreach (var chip in CategoryChips) FilteredCategoryChipOptions.Add(chip);
                return;
            }

            bool allIncluded = false;
            foreach (var chip in CategoryChips)
            {
                if (chip.DisplayName != null &&
                    chip.DisplayName.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    FilteredCategoryChipOptions.Add(chip);
                    if (chip == allChip) allIncluded = true;
                }
            }

            if (!allIncluded && allChip != null)
                FilteredCategoryChipOptions.Insert(0, allChip);
        }

        /// <summary>
        /// Real data reload — call this when the underlying data may have
        /// changed (a completed sale, theme swap forcing a redraw, language
        /// swap needing new chip labels). Every purely-filter-driven change
        /// (date pickers, payment/category chip selection) should call
        /// RecomputeAndRedraw directly instead — see that method.
        /// </summary>
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
                // Filtered to IsCurrent = 1 (2026-08-28, receipt revisioning -- see
                // DatabaseBootstrapper's matching comment): once a receipt has been
                // returned-from, its original bills row stays in the table as
                // history, superseded by a new row with the same Billnumber.
                // Reading every bill unfiltered here would double-count that
                // receipt's revenue/profit. Sells is filtered to match by BillId
                // membership, not Billnumber -- a superseded bill's own line items
                // share the SAME Billnumber as their replacement's, so a Billnumber
                // filter couldn't tell them apart the way a BillId one can.
                var currentBills = _billsData.ReadBills("bills").Where(b => b.IsCurrent).ToList();
                var currentBillIds = new HashSet<int>(currentBills.Select(b => b.Id));
                _cachedBills = currentBills;
                _cachedSells = _sellsData.ReadPendingSell("sells").Where(s => currentBillIds.Contains(s.BillId)).ToList();
                var customers = _customersData.ReadCustomers("customers");

                HasAnyBillsEver = _cachedBills.Count > 0;
                OutstandingDebtTotal = customers.Sum(c => c.Remain); // unfiltered — see field comment

                ChartLegendTextPaint = new SolidColorPaint(GetThemeColor("OnSurfaceColor", "#1C1A23"));
                ChartTooltipBackgroundPaint = new SolidColorPaint(GetThemeColor("SurfaceContainerHighestColor", "#E6E0ED"));
                ChartTooltipTextPaint = new SolidColorPaint(GetThemeColor("OnSurfaceColor", "#1C1A23"));

                RebuildCategoryChips(_cachedSells);

                RecomputeAndRedraw();

                LastUpdatedText = DateTime.Now.ToString("HH:mm:ss");
            }
            catch (Exception ex)
            {
                LastUpdatedText = "Error: " + ex.Message;
            }
        }

        /// <summary>
        /// Re-filters the already-loaded _cachedBills/_cachedSells and
        /// redraws the KPIs + filter-aware charts. Zero SQLite calls — this
        /// is what every date-picker edit and every chip click actually
        /// runs, per the Performance rules (filter changes must never
        /// re-hit the database).
        /// </summary>
        private void RecomputeAndRedraw()
        {
            ActiveRangeText = FormatRangeText(EffectiveStart, EffectiveEnd);
            ApplyFiltersAndRebuildCharts(_cachedBills, _cachedSells);
        }

        private static string FormatRangeText(DateTime start, DateTime end)
        {
            return start == end
                ? start.ToString("d MMM yyyy")
                : start.ToString("d MMM") + " – " + end.ToString("d MMM yyyy");
        }

        private static bool TryParseDatex(string datex, out DateTime date) =>
            DateTime.TryParseExact(datex, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

        /// <summary>
        /// Builds an Axis with theme-aware label text and gridline colors.
        /// Added 2026-08-24 — axes previously used plain `new Axis()`, which
        /// left LabelsPaint/SeparatorsPaint on LiveChartsCore's own default
        /// (effectively black-on-white), fine in light theme but reading as
        /// mismatched/illegible next to a dark card background (flagged via
        /// screenshot). Reuses the same GetThemeColor + SolidColorPaint
        /// pattern already proven for series Fill/Stroke and
        /// ChartLegendTextPaint elsewhere in this file, rather than a new
        /// approach — same confidence level as that existing code, not a
        /// new area of LiveCharts API risk. One shared helper instead of
        /// repeating this at each of the six axis call sites, so a future
        /// theme-color tweak can't accidentally miss one.
        /// </summary>
        private static Axis ThemedAxis(string[] labels = null, double? minLimit = null)
        {
            var axis = new Axis
            {
                LabelsPaint = new SolidColorPaint(GetThemeColor("OnSurfaceVariantColor", "#484554")),
                SeparatorsPaint = new SolidColorPaint(GetThemeColor("OutlineVariantColor", "#CAC4D7")) { StrokeThickness = 1 }
            };
            if (labels != null) axis.Labels = labels;
            if (minLimit.HasValue) axis.MinLimit = minLimit.Value;
            return axis;
        }

        /// <summary>
        /// The heart of the cross-filtering pipeline. KPIs, Top Items, the
        /// payment-split donut, and the category-revenue bar chart are ALL
        /// derived from the same `sellsFiltered` list built here — that's
        /// what makes combining filters behave correctly (e.g. "Cash sales
        /// of Beverages this week") instead of charts that each apply
        /// filters slightly differently and can disagree with each other.
        /// The revenue trend line also derives from `sellsFiltered`, bucketed
        /// by day or week (see BuildRevenueTrendChart). Top Items joined
        /// this pipeline 2026-08-25 — see BuildTopItemsChart's own comment
        /// for why it was originally the one exception and isn't anymore.
        /// </summary>
        private void ApplyFiltersAndRebuildCharts(List<Core.Models.Bills> allBills, List<Core.Models.Sells> allSells)
        {
            DateTime start = EffectiveStart;
            DateTime end = EffectiveEnd;

            var billNumberToPayment = allBills
                .GroupBy(b => b.Billnumber)
                .ToDictionary(g => g.Key, g => g.First().Details);

            // Discount (2026-09-04 fix) -- a Sells row's Price is always the
            // full, PRE-discount unit price (CheckoutViewModel.CompleteSale
            // writes line.Price as-is; the discount only ever gets applied
            // and stored at the Bills level, as DiscountPercent). Every
            // revenue/profit figure below therefore has to look up its
            // bill's discount and net it out itself -- summing Price*Quantity
            // directly (the previous behavior) silently ignored discounts
            // entirely, so a bill rung up as (say) $100 with a 20% discount
            // (an actual $80 sale) was counted as $100 everywhere on this
            // screen. Keyed by Billnumber, same as billNumberToPayment right
            // above, for the same reason (a superseded bill's line items
            // share their replacement's Billnumber, and allBills is already
            // filtered to IsCurrent rows only by the time it gets here).
            var billNumberToDiscountPercent = allBills
                .GroupBy(b => b.Billnumber)
                .ToDictionary(g => g.Key, g => g.First().DiscountPercent);

            string paymentFilter = SelectedPaymentChip?.Value;
            string categoryFilter = SelectedCategoryChip?.Value;

            var sellsFiltered = allSells.Where(s =>
            {
                if (!TryParseDatex(s.Datex, out DateTime d) || d.Date < start || d.Date > end) return false;
                if (categoryFilter != null && s.Category != categoryFilter) return false;
                if (paymentFilter != null &&
                    (!billNumberToPayment.TryGetValue(s.Billnumber, out string pm) || pm != paymentFilter)) return false;
                return true;
            }).ToList();

            FilteredRevenue = sellsFiltered.Sum(s => LineRevenue(s, billNumberToDiscountPercent));
            FilteredProfit = sellsFiltered.Sum(s => LineProfit(s, billNumberToDiscountPercent));
            FilteredTransactionCount = sellsFiltered.Select(s => s.Billnumber).Distinct().Count();

            BuildTopItemsChart(sellsFiltered, billNumberToDiscountPercent);
            BuildPaymentSplitChart(sellsFiltered, billNumberToPayment, billNumberToDiscountPercent);
            BuildCategoryRevenueChart(sellsFiltered, billNumberToDiscountPercent);
            BuildRevenueTrendChart(sellsFiltered, start, end, billNumberToDiscountPercent);
        }

        /// <summary>
        /// A Sells line's revenue AFTER its bill's discount -- see the
        /// discount comment in ApplyFiltersAndRebuildCharts above for why
        /// this can't just be s.Price * s.Quantity. Falls back to 0%
        /// discount for a Billnumber the map doesn't have (shouldn't happen
        /// in practice -- every Sells row is written alongside its bill in
        /// the same CompleteSale transaction -- but a missing lookup should
        /// degrade to the old undiscounted number, not throw or silently
        /// zero out real revenue).
        /// </summary>
        private static double LineRevenue(Core.Models.Sells s, Dictionary<int, double> billNumberToDiscountPercent)
        {
            double discountPercent = billNumberToDiscountPercent.TryGetValue(s.Billnumber, out double dp) ? dp : 0;
            return s.Price * s.Quantity * (1 - discountPercent / 100.0);
        }

        /// <summary>
        /// A Sells line's profit AFTER its bill's discount. s.Earned is
        /// stored PRE-discount ((Price - Cost) * Quantity, same as
        /// CheckoutViewModel.CompleteSale writes it) -- discount reduces
        /// what the customer actually paid without changing what the item
        /// cost, so the currency amount taken off comes straight out of
        /// profit rather than being split proportionally between cost and
        /// margin.
        /// </summary>
        private static double LineProfit(Core.Models.Sells s, Dictionary<int, double> billNumberToDiscountPercent)
        {
            double discountPercent = billNumberToDiscountPercent.TryGetValue(s.Billnumber, out double dp) ? dp : 0;
            return s.Earned - (s.Price * s.Quantity * discountPercent / 100.0);
        }

        private void BuildTopItemsChart(List<Core.Models.Sells> sellsFiltered, Dictionary<int, double> billNumberToDiscountPercent)
        {
            // Changed 2026-08-25: this WAS deliberately all-time and
            // unfiltered (see Dashboard-Parity-Plan.md's "Design deviations"
            // / "Explicitly NOT filtered" notes for the original reasoning —
            // now historical, not current behavior). Mahmoud asked for it to
            // respect the active filters instead, on the grounds that the
            // All Time quick-range button already covers the
            // "what sells best, ever" case explicitly — so a permanently-
            // unfiltered exception isn't needed alongside it. Now takes
            // sellsFiltered (the same filtered set every other chart on this
            // screen uses) and is called from ApplyFiltersAndRebuildCharts,
            // not RefreshDashboard — so it's now part of the same
            // cross-filter pipeline as Payment Split, Category Revenue, and
            // Revenue Trend, instead of being the one standing exception.
            //
            // Carries Revenue and Profit (Quantity * Price, and Earned,
            // summed) per item too, so the hover tooltip can show both
            // numbers alongside Quantity — see TopItemPoint.cs.
            var topItems = sellsFiltered
                .GroupBy(s => s.Name)
                .Select(g => new TopItemPoint
                {
                    Name = g.Key,
                    Quantity = g.Sum(s => s.Quantity),
                    Revenue = g.Sum(s => LineRevenue(s, billNumberToDiscountPercent)),
                    Profit = g.Sum(s => LineProfit(s, billNumberToDiscountPercent))
                })
                .OrderByDescending(x => x.Quantity)
                .Take(5)
                .ToList();

            var primary = GetThemeColor("PrimaryColor", "#6C4CE0");
            string unitsSoldLabel = LocalizationManager.GetString("DashboardUnitsSoldLabel");
            string revenueLabel = LocalizationManager.GetString("DashboardItemRevenueLabel");
            string profitLabel = LocalizationManager.GetString("DashboardTodayProfit");

            TopItemsSeries.Clear();
            TopItemsSeries.Add(new ColumnSeries<TopItemPoint>
            {
                Values = topItems,
                Name = "Units sold",
                Fill = new SolidColorPaint(primary),
                MaxBarWidth = 42,
                // Mapping tells LiveCharts how to turn each TopItemPoint into
                // a plotted (X, Y) Coordinate: X is just the bar's position
                // (the `index` parameter, same order as the Labels array
                // below), Y is Quantity — same bar heights as before this
                // change, only the underlying model type changed.
                //
                // NOTE (2026-08-24): the installed LiveChartsCore version's
                // Mapping signature is Func<TopItemPoint, int, Coordinate> —
                // (model, index) => Coordinate — not the older
                // Action<TModel, ChartPoint> style that set point.PrimaryValue/
                // SecondaryValue directly. That older form is what caused the
                // CS0246/CS1643/CS1061 build errors (the compiler correctly
                // inferred `point` as the int index, which has no
                // PrimaryValue/SecondaryValue/Context members). Fixed to match
                // the real installed API surface, flagging per the standing
                // rule that this package's exact API is the least-certain part
                // of this file.
                Mapping = (item, index) => new Coordinate(index, item.Quantity),
                // The actual point of the model change: point.Model gives
                // back the original TopItemPoint for whichever bar is being
                // hovered, so the tooltip can show Revenue and Profit
                // alongside the Quantity that was already there.
                YToolTipLabelFormatter = point =>
                    $"{unitsSoldLabel}: {point.Model.Quantity:0}\n{revenueLabel}: {point.Model.Revenue:0.00}\n{profitLabel}: {point.Model.Profit:0.00}"
            });

            TopItemsXAxes = new[] { ThemedAxis(labels: topItems.Select(x => x.Name).ToArray()) };
            TopItemsYAxes = new[] { ThemedAxis(minLimit: 0) };
        }

        private void BuildPaymentSplitChart(List<Core.Models.Sells> sellsFiltered, Dictionary<int, string> billNumberToPayment, Dictionary<int, double> billNumberToDiscountPercent)
        {
            double RevenueFor(string tag) => sellsFiltered
                .Where(s => billNumberToPayment.TryGetValue(s.Billnumber, out string pm) && pm == tag)
                .Sum(s => LineRevenue(s, billNumberToDiscountPercent));

            double cashTotal = RevenueFor("Cash");
            double cardTotal = RevenueFor("Card");
            double creditTotal = RevenueFor("Credit");

            var primary = GetThemeColor("PrimaryColor", "#6C4CE0");
            // NOT SecondaryColor: Material's Fidelity scheme deliberately
            // derives Secondary as a muted/toned-down variant of the SAME hue
            // as Primary (that's the whole point of the role in Material
            // Design 3) — fine for a UI accent, but it's why the "Card" slice
            // read as barely-different purple next to "Cash" in the
            // screenshot. In dark theme the two are even literally identical
            // (Colors.Dark.xaml has PrimaryColor == SecondaryColor ==
            // #CBBEFF) — not a copy-paste bug, just this Material role doing
            // what it's specified to do, which happens to be the wrong tool
            // for "make two chart slices visually distinct." InversePrimary
            // is built by the same engine specifically to contrast against
            // Primary (swapped lightness in both themes), so it reads as a
            // genuinely different color at a glance in both light and dark —
            // reuses an existing token rather than inventing a new one, per
            // the design-system "no new colors" rule.
            var secondary = GetThemeColor("InversePrimaryColor", "#CBBEFF");
            var tertiary = GetThemeColor("TertiaryColor", "#7C3F00");

            string cashLabel = LocalizationManager.GetString("CheckoutCash");
            string cardLabel = LocalizationManager.GetString("CheckoutCard");
            string creditLabel = LocalizationManager.GetString("CheckoutPayLater");

            // Percentage-of-total alongside each legend label (requested
            // 2026-08-24). LiveChartsCore's legend renders each series'
            // Name verbatim — no separate legend-label template API to hook
            // into (same low-risk-API reasoning as the rest of this file's
            // LiveCharts usage) — so the percentage is folded directly into
            // Name here rather than attempting a custom legend item
            // template, which would be new, unverified chart-API surface.
            // Computed from cashTotal/cardTotal/creditTotal (the same
            // filtered totals the slices themselves are built from), so the
            // percentage always matches whatever filter is active — never a
            // separate, potentially-stale calculation.
            double totalRevenue = cashTotal + cardTotal + creditTotal;
            string PctSuffix(double part) => totalRevenue > 0
                ? $" ({Math.Round(part / totalRevenue * 100).ToString(CultureInfo.InvariantCulture)}%)"
                : " (0%)";

            PaymentSplitSeries.Clear();

            // Full circle, not a donut — InnerRadius omitted (defaults to 0).
            if (cashTotal > 0)
                PaymentSplitSeries.Add(new PieSeries<double> { Values = new[] { cashTotal }, Name = cashLabel + PctSuffix(cashTotal), Fill = new SolidColorPaint(primary) });
            if (cardTotal > 0)
                PaymentSplitSeries.Add(new PieSeries<double> { Values = new[] { cardTotal }, Name = cardLabel + PctSuffix(cardTotal), Fill = new SolidColorPaint(secondary) });
            if (creditTotal > 0)
                PaymentSplitSeries.Add(new PieSeries<double> { Values = new[] { creditTotal }, Name = creditLabel + PctSuffix(creditTotal), Fill = new SolidColorPaint(tertiary) });
        }

        private void BuildCategoryRevenueChart(List<Core.Models.Sells> sellsFiltered, Dictionary<int, double> billNumberToDiscountPercent)
        {
            var categoryTotals = sellsFiltered
                .GroupBy(s => string.IsNullOrWhiteSpace(s.Category) ? "—" : s.Category)
                .Select(g => new CategoryRevenuePoint
                {
                    Category = g.Key,
                    Quantity = g.Sum(s => s.Quantity),
                    Revenue = g.Sum(s => LineRevenue(s, billNumberToDiscountPercent)),
                    Profit = g.Sum(s => LineProfit(s, billNumberToDiscountPercent))
                })
                .OrderByDescending(x => x.Revenue)
                .ToList();

            var primary = GetThemeColor("PrimaryColor", "#6C4CE0");
            string unitsSoldLabel = LocalizationManager.GetString("DashboardUnitsSoldLabel");
            string revenueLabel = LocalizationManager.GetString("DashboardItemRevenueLabel");
            string profitLabel = LocalizationManager.GetString("DashboardTodayProfit");

            CategoryRevenueSeries.Clear();
            CategoryRevenueSeries.Add(new ColumnSeries<CategoryRevenuePoint>
            {
                Values = categoryTotals,
                Name = LocalizationManager.GetString("DashboardCategoryRevenueTitle"),
                Fill = new SolidColorPaint(primary),
                MaxBarWidth = 56,
                // Same Mapping/tooltip pattern as TopItemsSeries above — see
                // that Mapping's comment for why this is (model, index) =>
                // Coordinate rather than the older ChartPoint-mutation form.
                Mapping = (item, index) => new Coordinate(index, item.Revenue),
                YToolTipLabelFormatter = point =>
                    $"{unitsSoldLabel}: {point.Model.Quantity:0}\n{revenueLabel}: {point.Model.Revenue:0.00}\n{profitLabel}: {point.Model.Profit:0.00}"
            });

            CategoryRevenueXAxes = new[] { ThemedAxis(labels: categoryTotals.Select(x => x.Category).ToArray()) };
            CategoryRevenueYAxes = new[] { ThemedAxis(minLimit: 0) };
        }

        private void BuildRevenueTrendChart(List<Core.Models.Sells> sellsFiltered, DateTime start, DateTime end, Dictionary<int, double> billNumberToDiscountPercent)
        {
            int totalDays = (end - start).Days + 1;
            // Performance rule (Dashboard-Parity-Plan.md): cap chart data
            // points on old hardware — a custom range longer than ~2 months
            // buckets by week instead of by day, so a full year selected
            // still renders ~52 points, not ~365.
            bool aggregateWeekly = totalDays > 60;
            int bucketDays = aggregateWeekly ? 7 : 1;

            // Revenue and Profit tracked per bucket now (Profit added so the
            // hover tooltip can show both, same as TopItemsSeries and
            // CategoryRevenueSeries) — hence RevenueTrendPoint instead of a
            // plain double per bucket.
            var buckets = new SortedDictionary<DateTime, RevenueTrendPoint>();
            for (DateTime d = start; d <= end; d = d.AddDays(bucketDays))
                buckets[d] = new RevenueTrendPoint();

            foreach (var sell in sellsFiltered)
            {
                if (!TryParseDatex(sell.Datex, out DateTime d)) continue;
                DateTime bucketKey = aggregateWeekly
                    ? start.AddDays(((d.Date - start).Days / 7) * 7)
                    : d.Date;
                if (buckets.TryGetValue(bucketKey, out RevenueTrendPoint point))
                {
                    point.Revenue += LineRevenue(sell, billNumberToDiscountPercent);
                    point.Profit += LineProfit(sell, billNumberToDiscountPercent);
                }
            }

            var keys = buckets.Keys.ToList();
            var values = buckets.Values.ToList();
            // Usability rule: don't overcrowd — thin labels to roughly 8
            // regardless of range length or bucket size, always including
            // the last (most recent) point so "now" is never unlabeled.
            int labelEvery = Math.Max(1, keys.Count / 8);
            var labels = new string[keys.Count];
            for (int i = 0; i < keys.Count; i++)
                labels[i] = (i % labelEvery == 0 || i == keys.Count - 1) ? keys[i].ToString("d/M") : "";

            var primary = GetThemeColor("PrimaryColor", "#6C4CE0");
            string revenueLabel = LocalizationManager.GetString("DashboardItemRevenueLabel");
            string profitLabel = LocalizationManager.GetString("DashboardTodayProfit");

            RevenueTrendSeries.Clear();
            RevenueTrendSeries.Add(new LineSeries<RevenueTrendPoint>
            {
                Values = values,
                Name = LocalizationManager.GetString("DashboardRevenueTrendTitle"),
                Stroke = new SolidColorPaint(primary, 3),
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0,
                // Same Mapping/tooltip pattern as TopItemsSeries — see that
                // Mapping's comment for why this is (model, index) =>
                // Coordinate rather than the older ChartPoint-mutation form.
                Mapping = (item, index) => new Coordinate(index, item.Revenue),
                YToolTipLabelFormatter = point =>
                    $"{revenueLabel}: {point.Model.Revenue:0.00}\n{profitLabel}: {point.Model.Profit:0.00}"
            });

            RevenueTrendXAxes = new[] { ThemedAxis(labels: labels) };
            RevenueTrendYAxes = new[] { ThemedAxis(minLimit: 0) };
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
