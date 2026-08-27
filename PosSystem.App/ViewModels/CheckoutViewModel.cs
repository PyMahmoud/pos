using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Windows.Input;
using PosSystem.App.Localization;
using PosSystem.Core.Models;

namespace PosSystem.App.ViewModels
{
    public enum PaymentMethod
    {
        Cash,
        Card,
        // Phase 5 addition. Only reachable when a real customer (not
        // Walk-in) is selected — see CanPayLater / the guard at the top of
        // CompleteSale(). Leaves the bill partially/fully unpaid and adds
        // the difference to the linked customer's Remain.
        PayLater
    }

    /// <summary>
    /// DataContext for CheckoutView. Loads all goods once on construction
    /// and filters client-side by category + search text — 281 rows is
    /// nothing to filter in memory, and it means typing in the search box
    /// doesn't round-trip SQLite on every keystroke.
    ///
    /// On Complete Sale: one Bills row for the whole order, one Sells row
    /// per line, and each sold good's Quantity decremented — all via the
    /// existing Core.Data layer, no new SQL added here.
    ///
    /// Every completed sale settles in full UNLESS Pay Later is selected
    /// (Phase 5): Cash and Card are still both immediately-settled tender
    /// types (no payment gateway — staff key in whatever the card reader
    /// showed as a logged payment), but a sale can now optionally be linked
    /// to a customer via the picker either way. Linking a customer under
    /// Cash/Card just records who the sale was for (Paid grows on their
    /// running total, Remain doesn't); linking under Pay Later is the
    /// actual "buy now, pay later" flow — Remain grows by the full total
    /// instead, and gets paid down later from the Customers screen.
    /// </summary>
    public class CheckoutViewModel : ViewModelBase
    {
        private readonly Core.Data.Goods _goodsData = new Core.Data.Goods();
        private readonly Core.Data.Bills _billsData = new Core.Data.Bills();
        private readonly Core.Data.Sells _sellsData = new Core.Data.Sells();
        private readonly Core.Data.Customers _customersData = new Core.Data.Customers();

        private List<GoodsR> _allGoods = new List<GoodsR>();

        public ObservableCollection<GoodsR> FilteredGoods { get; } = new ObservableCollection<GoodsR>();
        public ObservableCollection<CategoryChip> Categories { get; } = new ObservableCollection<CategoryChip>();

        // Type-to-search category dropdown (2026-08-26). FilteredCategoryOptions
        // is what the ComboBox's ItemsSource actually binds to now — Categories
        // above stays the full master list, untouched, and is still exactly
        // what RebuildCategoryChips populates. Typing in the (now-editable)
        // ComboBox drives CategorySearchText, which narrows
        // FilteredCategoryOptions to matching DisplayNames; "All" is always
        // kept reachable even when it doesn't itself match the typed text, so
        // a search that matches nothing never leaves the dropdown with no way
        // back to "show everything" (see RebuildFilteredCategories). Selecting
        // an item goes through the normal SelectedCategory setter below,
        // which pushes that item's DisplayName into CategorySearchText
        // WITHOUT re-filtering (_suppressCategoryFilter) — only real typing
        // re-filters, a selection never does.
        public ObservableCollection<CategoryChip> FilteredCategoryOptions { get; } = new ObservableCollection<CategoryChip>();

        private bool _suppressCategoryFilter;

        private string _categorySearchText = "";
        public string CategorySearchText
        {
            get => _categorySearchText;
            set
            {
                if (!SetProperty(ref _categorySearchText, value)) return;
                if (_suppressCategoryFilter) return;
                RebuildFilteredCategories();
                IsCategoryDropDownOpen = true;
            }
        }

        // Bound TwoWay to the ComboBox's IsDropDownOpen. WPF doesn't open an
        // editable ComboBox's popup just because its Text changed (that only
        // happens on a manual click/F4/Alt+Down) — this is set explicitly
        // from CategorySearchText's setter above whenever the user actually
        // types, and gets reset back by the ComboBox itself (via the TwoWay
        // binding) whenever a selection is made or the dropdown closes some
        // other way.
        private bool _isCategoryDropDownOpen;
        public bool IsCategoryDropDownOpen
        {
            get => _isCategoryDropDownOpen;
            set => SetProperty(ref _isCategoryDropDownOpen, value);
        }

        public ObservableCollection<CartLine> CartLines { get; } = new ObservableCollection<CartLine>();
        public ObservableCollection<CustomerOption> Customers { get; } = new ObservableCollection<CustomerOption>();

        private CategoryChip _selectedCategory;
        public CategoryChip SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (!SetProperty(ref _selectedCategory, value)) return;
                ApplyFilter();

                // Keep the search box showing the selected category's name
                // without re-triggering RebuildFilteredCategories — see
                // CategorySearchText's own comment above.
                _suppressCategoryFilter = true;
                CategorySearchText = value?.DisplayName ?? "";
                _suppressCategoryFilter = false;
            }
        }

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value)) ApplyFilter();
            }
        }

        private CustomerOption _selectedCustomer;
        public CustomerOption SelectedCustomer
        {
            get => _selectedCustomer;
            set
            {
                if (!SetProperty(ref _selectedCustomer, value)) return;
                OnPropertyChanged(nameof(CanPayLater));

                // Walk-in (Model == null) can't carry a tab — fall back to
                // Cash automatically rather than leaving Pay Later selected
                // with nothing to attach it to.
                if (!CanPayLater && SelectedPaymentMethod == PaymentMethod.PayLater)
                    SelectedPaymentMethod = PaymentMethod.Cash;
            }
        }

        public bool CanPayLater => SelectedCustomer?.Model != null;

        private PaymentMethod _selectedPaymentMethod = PaymentMethod.Cash;
        public PaymentMethod SelectedPaymentMethod
        {
            get => _selectedPaymentMethod;
            set
            {
                if (SetProperty(ref _selectedPaymentMethod, value))
                {
                    OnPropertyChanged(nameof(IsCashSelected));
                    OnPropertyChanged(nameof(IsCardSelected));
                    OnPropertyChanged(nameof(IsPayLaterSelected));
                }
            }
        }

        public bool IsCashSelected => SelectedPaymentMethod == PaymentMethod.Cash;
        public bool IsCardSelected => SelectedPaymentMethod == PaymentMethod.Card;
        public bool IsPayLaterSelected => SelectedPaymentMethod == PaymentMethod.PayLater;

        public double Subtotal => CartLines.Sum(l => l.LineTotal);

        // Settings-driven as of 2026-08-26 (AppSettings.TaxRatePercent) —
        // this is the "gets added there when the client needs it" this
        // property's own comment used to point at; see AppSettings' class
        // doc comment for the full history. 0% (AppSettings' default)
        // reproduces the exact old behavior, so a shop that never touches
        // the new Settings field sees no change at all.
        public double TaxAmount => Math.Round(Subtotal * AppSettings.TaxRatePercent / 100.0, 2);

        // Discount is still genuinely unimplemented (no UI anywhere sets
        // one) — only Tax moved off the placeholder above.
        public double Total => Subtotal + TaxAmount;

        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ICommand SetPaymentMethodCommand { get; }
        public ICommand AddToCartCommand { get; }
        public ICommand IncrementLineCommand { get; }
        public ICommand DecrementLineCommand { get; }
        public ICommand RemoveLineCommand { get; }
        public ICommand CompleteSaleCommand { get; }

        public CheckoutViewModel()
        {
            SetPaymentMethodCommand = new RelayCommand(p =>
            {
                if (p is PaymentMethod method && (method != PaymentMethod.PayLater || CanPayLater))
                    SelectedPaymentMethod = method;
            });
            AddToCartCommand = new RelayCommand(p => AddToCart(p as GoodsR));
            IncrementLineCommand = new RelayCommand(p =>
            {
                if (p is CartLine line) line.Quantity += 1;
            });
            DecrementLineCommand = new RelayCommand(p =>
            {
                if (!(p is CartLine line)) return;
                if (line.Quantity <= 1) CartLines.Remove(line);
                else line.Quantity -= 1;
                RaiseTotals();
            });
            RemoveLineCommand = new RelayCommand(p =>
            {
                if (p is CartLine line) CartLines.Remove(line);
                RaiseTotals();
            });
            CompleteSaleCommand = new RelayCommand(_ => CompleteSale());

            CartLines.CollectionChanged += (s, e) => RaiseTotals();
            LocalizationManager.LanguageChanged += _ =>
            {
                RebuildCategoryChips();
                RebuildCustomerOptions();
            };
            CustomerDataEvents.CustomersChanged += RebuildCustomerOptions;
            // Phase 7: a direct quantity adjustment on the Inventory screen
            // needs to show up here too — same cross-screen-freshness
            // reasoning as the CustomerDataEvents subscription just above.
            // LoadGoods() below already fires this same event after a
            // completed sale, so this screen's own writes don't cause a
            // problematic self-reload loop — just one extra (harmless) pass,
            // same as CustomersViewModel/RecordPayment already does today.
            InventoryDataEvents.GoodsChanged += LoadGoods;

            // Settings screen (2026-08-26): a saved Tax Rate change needs
            // to repaint the Total a mid-shift cashier is currently looking
            // at, not just apply to sales rung up after the next app
            // restart. RaiseTotals just re-raises PropertyChanged for
            // Subtotal/TaxAmount/Total against the current cart -- no data
            // reload needed, same reasoning as why CartLines.CollectionChanged
            // already calls it above.
            AppSettings.Changed += RaiseTotals;

            LoadGoods();
            RebuildCustomerOptions();
        }

        private void RaiseTotals()
        {
            OnPropertyChanged(nameof(Subtotal));
            OnPropertyChanged(nameof(TaxAmount));
            OnPropertyChanged(nameof(Total));
        }

        private void LoadGoods()
        {
            _allGoods = _goodsData.ReadAllGoodsRPic("goods");
            RebuildCategoryChips();
            ApplyFilter();
        }

        private void RebuildCategoryChips()
        {
            string previousValue = SelectedCategory?.Value;

            Categories.Clear();
            Categories.Add(new CategoryChip
            {
                DisplayName = LocalizationManager.GetString("CheckoutAllCategory"),
                Value = null
            });
            foreach (var category in _allGoods
                         .Select(g => g.Category)
                         .Where(c => !string.IsNullOrWhiteSpace(c))
                         .Distinct()
                         .OrderBy(c => c))
            {
                Categories.Add(new CategoryChip { DisplayName = category, Value = category });
            }

            _selectedCategory = Categories.FirstOrDefault(c => c.Value == previousValue) ?? Categories[0];
            OnPropertyChanged(nameof(SelectedCategory));

            // RebuildCategoryChips sets _selectedCategory directly (not
            // through the SelectedCategory property setter above), so the
            // search-box sync that setter normally does has to happen here
            // too — otherwise the box would keep showing stale text after a
            // data reload or language change rebuilds the whole Categories
            // list.
            _suppressCategoryFilter = true;
            CategorySearchText = _selectedCategory?.DisplayName ?? "";
            _suppressCategoryFilter = false;
            RebuildFilteredCategories();
        }

        // See FilteredCategoryOptions' doc comment above for the overall
        // design. "All" (Categories[0], Value == null) is always kept
        // reachable even when the typed text doesn't match its own label, so
        // a search that matches nothing still leaves a way back to seeing
        // everything.
        private void RebuildFilteredCategories()
        {
            FilteredCategoryOptions.Clear();
            string text = CategorySearchText ?? "";
            CategoryChip allChip = Categories.Count > 0 ? Categories[0] : null;

            if (string.IsNullOrWhiteSpace(text))
            {
                foreach (var chip in Categories) FilteredCategoryOptions.Add(chip);
                return;
            }

            bool allIncluded = false;
            foreach (var chip in Categories)
            {
                if (chip.DisplayName != null &&
                    chip.DisplayName.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    FilteredCategoryOptions.Add(chip);
                    if (chip == allChip) allIncluded = true;
                }
            }

            if (!allIncluded && allChip != null)
                FilteredCategoryOptions.Insert(0, allChip);
        }

        private void RebuildCustomerOptions()
        {
            int? previousId = SelectedCustomer?.Model?.Id;

            Customers.Clear();
            Customers.Add(new CustomerOption
            {
                DisplayName = LocalizationManager.GetString("CheckoutWalkIn"),
                Model = null
            });
            foreach (var customer in _customersData.ReadCustomers("customers").OrderBy(c => c.Ownername))
            {
                Customers.Add(new CustomerOption { DisplayName = customer.Ownername, Model = customer });
            }

            _selectedCustomer = previousId.HasValue
                ? Customers.FirstOrDefault(c => c.Model?.Id == previousId.Value) ?? Customers[0]
                : Customers[0];
            OnPropertyChanged(nameof(SelectedCustomer));
            OnPropertyChanged(nameof(CanPayLater));
        }

        private void ApplyFilter()
        {
            IEnumerable<GoodsR> query = _allGoods;

            if (SelectedCategory?.Value != null)
                query = query.Where(g => g.Category == SelectedCategory.Value);

            if (!string.IsNullOrWhiteSpace(SearchText))
                query = query.Where(g =>
                    g.Name != null && g.Name.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0);

            FilteredGoods.Clear();
            foreach (var good in query) FilteredGoods.Add(good);
        }

        private void AddToCart(GoodsR good)
        {
            if (good == null || good.Quantity <= 0) return;

            var existing = CartLines.FirstOrDefault(l => l.GoodId == good.Id);
            if (existing != null)
            {
                if (existing.Quantity < existing.MaxAvailable) existing.Quantity += 1;
            }
            else
            {
                CartLines.Add(new CartLine(good, 1));
            }
            RaiseTotals();
        }

        private void CompleteSale()
        {
            if (CartLines.Count == 0) return;

            if (SelectedPaymentMethod == PaymentMethod.PayLater && !CanPayLater)
            {
                // Guard only — the Pay Later button is disabled in this
                // state (see CheckoutView.xaml IsEnabled binding), this just
                // prevents a bad write if it's ever reached another way.
                StatusMessage = LocalizationManager.GetString("CheckoutPayLaterRequiresCustomer");
                return;
            }

            try
            {
                DateTime now = DateTime.Now;
                string time = now.ToString("HH:mm");
                string date = now.ToString("dd/MM/yyyy");

                // Bills.InsertBills requires an explicit ID (not just
                // Billnumber) — there's no dedicated "next ID" helper in
                // Core, so compute both from the existing table rather than
                // adding new SQL to a file Mahmoud might also be touching.
                // DBNull-guarded: Convert.ToInt32 throws on a NULL cell
                // ("Object cannot be cast from DBNull to other types"), and
                // a bill row missing ID/Billnumber shouldn't block every
                // future sale from saving — treat it as 0 and move on.
                DataTable billsTable = _billsData.ReadAdapter("bills");
                int nextId = 1;
                int nextBillNumber = 1000;
                foreach (DataRow row in billsTable.Rows)
                {
                    int rowId = SafeInt(row["ID"]);
                    int rowBillNumber = SafeInt(row["Billnumber"]);
                    if (rowId >= nextId) nextId = rowId + 1;
                    if (rowBillNumber >= nextBillNumber) nextBillNumber = rowBillNumber + 1;
                }

                double totalCost = Total;
                double totalEarned = CartLines.Sum(l => (l.Price - l.Cost) * l.Quantity);

                var linkedCustomer = SelectedCustomer?.Model;
                bool isPayLater = SelectedPaymentMethod == PaymentMethod.PayLater;

                string ownername = linkedCustomer?.Ownername ?? "";
                string ownerid = linkedCustomer?.Ownerid ?? "";
                string ownernumber = linkedCustomer?.Ownernumber ?? "";

                // Cash/Card settle the bill in full immediately (Paid =
                // total, Remain = 0) — exactly what this screen always did,
                // whether or not a customer happens to be linked. Pay Later
                // is the one case that leaves a balance: nothing collected
                // now, the whole total goes on the linked customer's tab.
                double billPaid = isPayLater ? 0 : totalCost;
                double billRemain = isPayLater ? totalCost : 0;
                string paymentTag = SelectedPaymentMethod == PaymentMethod.Cash ? "Cash"
                                   : SelectedPaymentMethod == PaymentMethod.Card ? "Card"
                                   : "Credit";

                // Tax (Settings-driven as of 2026-08-26, see TaxAmount's
                // doc comment) is the actual amount collected as tax on
                // this bill; Discount stays 0 — still genuinely
                // unimplemented, no UI anywhere sets one yet.
                _billsData.InsertBills(
                    "bills", nextId, nextBillNumber, totalCost, time, date,
                    ownername, ownerid, ownernumber,
                    billPaid, billRemain, totalEarned, TaxAmount, 0, paymentTag,
                    linkedCustomer?.Id);

                foreach (var line in CartLines)
                {
                    _sellsData.InsertSells(
                        "sells", line.Name, line.Category, line.Quantity, line.Cost, line.Price,
                        line.Type, time, date, line.Barcode, nextBillNumber,
                        (line.Price - line.Cost) * line.Quantity, "No", "");

                    // ID-based, not Barcode-based (Phase 7): a product's
                    // Barcode can now be "" (no barcode), which is no
                    // longer guaranteed unique — see
                    // Data.Goods.UpdateGoodCountById's doc comment.
                    double newQuantity = line.MaxAvailable - line.Quantity;
                    _goodsData.UpdateGoodCountById("goods", line.GoodId, newQuantity);
                }

                // Linked customer's running totals: Paid grows by whatever
                // was actually collected now, Remain grows by whatever
                // wasn't (0 for Cash/Card, the full total for Pay Later).
                // See CustomersViewModel.RecordPayment for how a later
                // payment brings Remain back down.
                if (linkedCustomer != null)
                {
                    double newCustomerPaid = linkedCustomer.Paid + billPaid;
                    double newCustomerRemain = linkedCustomer.Remain + billRemain;
                    _customersData.UpdateCustomers(
                        "customers", linkedCustomer.Id, linkedCustomer.Ownername,
                        linkedCustomer.Ownerid, linkedCustomer.Ownernumber,
                        newCustomerPaid, newCustomerRemain);
                }

                StatusMessage = isPayLater
                    ? string.Format(LocalizationManager.GetString("CheckoutSaleSuccessCredit"), nextBillNumber, ownername)
                    : string.Format(LocalizationManager.GetString("CheckoutSaleSuccess"), nextBillNumber);

                CartLines.Clear();
                LoadGoods();

                // Phase 7: Inventory needs to know its cached Quantity
                // values just went stale, same as this event already tells
                // Checkout when Inventory adjusts stock directly.
                InventoryDataEvents.RaiseGoodsChanged();

                // Phase 6: every completed sale (not just customer-linked
                // ones) needs to refresh Dashboard's KPIs/charts.
                OrderEvents.RaiseOrderCompleted();

                if (linkedCustomer != null)
                {
                    CustomerDataEvents.RaiseCustomersChanged();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationManager.GetString("CheckoutSaleError") + " (" + ex.Message + ")";
            }
        }

        // DataRow returns DBNull.Value (not C# null) for a null cell —
        // Convert.ToInt32(DBNull.Value) throws "Object cannot be cast from
        // DBNull to other types", which is exactly what surfaced here.
        private static int SafeInt(object value) =>
            value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
    }
}
