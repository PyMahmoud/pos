using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using PosSystem.App.Localization;

namespace PosSystem.App.ViewModels
{
    /// <summary>
    /// DataContext for CustomersView. Loads the customers table once (8
    /// seeded rows today — trivially small, same "load it all, filter in
    /// memory" approach CheckoutViewModel already uses for 281 goods) and
    /// exposes: add a new customer, search/filter, and record a payment
    /// against an existing customer's balance.
    ///
    /// Reloads on CustomerDataEvents.CustomersChanged — that's how a Pay
    /// Later sale completed on the Checkout screen (which increases a
    /// customer's Remain) shows up here even though this screen wasn't the
    /// active tab when it happened. This ViewModel also raises that same
    /// event after its own writes, so Checkout's customer picker stays
    /// current too.
    /// </summary>
    public class CustomersViewModel : ViewModelBase
    {
        private readonly Core.Data.Customers _customersData = new Core.Data.Customers();

        private List<CustomerRow> _allCustomers = new List<CustomerRow>();
        public ObservableCollection<CustomerRow> Customers { get; } = new ObservableCollection<CustomerRow>();

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set { if (SetProperty(ref _searchText, value)) ApplyFilter(); }
        }

        private string _newCustomerName = "";
        public string NewCustomerName
        {
            get => _newCustomerName;
            set => SetProperty(ref _newCustomerName, value);
        }

        private string _newCustomerPhone = "";
        public string NewCustomerPhone
        {
            get => _newCustomerPhone;
            set => SetProperty(ref _newCustomerPhone, value);
        }

        private string _newCustomerCode = "";
        public string NewCustomerCode
        {
            get => _newCustomerCode;
            set => SetProperty(ref _newCustomerCode, value);
        }

        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        // Exists only so the balance badge's MultiBinding (CustomersView.xaml)
        // has something to re-trigger on when the language toggles.
        // CustomerRow.Remain alone wouldn't cause the localized "Paid up" /
        // "Owes {0}" text to re-resolve, and subscribing each CustomerRow to
        // LocalizationManager.LanguageChanged directly would leak — this
        // ViewModel is a singleton for the app's lifetime (MainViewModel
        // caches every screen forever), so one subscription here is safe
        // where dozens of short-lived CustomerRow instances would not be.
        private AppLanguage _currentLanguage = LocalizationManager.Current;
        public AppLanguage CurrentLanguage
        {
            get => _currentLanguage;
            private set => SetProperty(ref _currentLanguage, value);
        }

        public ICommand AddCustomerCommand { get; }
        public ICommand RecordPaymentCommand { get; }
        public ICommand ViewDetailsCommand { get; }

        private CustomerDetailViewModel _selectedDetail;
        public CustomerDetailViewModel SelectedDetail
        {
            get => _selectedDetail;
            private set => SetProperty(ref _selectedDetail, value);
        }

        public CustomersViewModel()
        {
            AddCustomerCommand = new RelayCommand(_ => AddCustomer());
            RecordPaymentCommand = new RelayCommand(p =>
            {
                if (p is CustomerRow row) RecordPayment(row);
            });
            ViewDetailsCommand = new RelayCommand(p =>
            {
                if (!(p is CustomerRow row)) return;

                var model = new Core.Models.Customers(row.Id, row.Ownername, row.Ownerid, row.Ownernumber, row.Paid, row.Remain);
                var detail = new CustomerDetailViewModel(model);
                detail.CloseRequested += () => SelectedDetail = null;
                SelectedDetail = detail;
            });

            CustomerDataEvents.CustomersChanged += LoadCustomers;
            LocalizationManager.LanguageChanged += lang => CurrentLanguage = lang;

            LoadCustomers();
        }

        private void LoadCustomers()
        {
            var models = _customersData.ReadCustomers("customers");
            _allCustomers = models.Select(m => new CustomerRow(m)).ToList();
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            IEnumerable<CustomerRow> query = _allCustomers;

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                query = query.Where(c =>
                    (c.Ownername != null && c.Ownername.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (c.Ownernumber != null && c.Ownernumber.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (c.Ownerid != null && c.Ownerid.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            Customers.Clear();
            foreach (var row in query.OrderBy(c => c.Ownername)) Customers.Add(row);
        }

        private void AddCustomer()
        {
            if (string.IsNullOrWhiteSpace(NewCustomerName))
            {
                StatusMessage = LocalizationManager.GetString("CustomersAddMissingName");
                return;
            }

            try
            {
                _customersData.InsertCustomers(
                    "customers", NewCustomerName.Trim(), NewCustomerCode.Trim(),
                    NewCustomerPhone.Trim(), 0, 0);

                NewCustomerName = "";
                NewCustomerPhone = "";
                NewCustomerCode = "";
                StatusMessage = LocalizationManager.GetString("CustomersAddSuccess");

                CustomerDataEvents.RaiseCustomersChanged();
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationManager.GetString("CustomersAddError") + " (" + ex.Message + ")";
            }
        }

        private void RecordPayment(CustomerRow row)
        {
            if (!double.TryParse(row.PaymentInput, out double amount) || amount <= 0)
            {
                StatusMessage = LocalizationManager.GetString("CustomersPaymentInvalid");
                return;
            }

            // Cap at the outstanding balance rather than rejecting an
            // over-entry — a customer paying off a rounded-up amount
            // shouldn't block clearing their tab.
            if (amount > row.Remain) amount = row.Remain;

            try
            {
                double newPaid = row.Paid + amount;
                double newRemain = row.Remain - amount;

                _customersData.UpdateCustomers(
                    "customers", row.Id, row.Ownername, row.Ownerid, row.Ownernumber,
                    newPaid, newRemain);

                StatusMessage = string.Format(LocalizationManager.GetString("CustomersPaymentSuccess"), row.Ownername);

                CustomerDataEvents.RaiseCustomersChanged();
            }
            catch (Exception ex)
            {
                StatusMessage = LocalizationManager.GetString("CustomersAddError") + " (" + ex.Message + ")";
            }
        }
    }
}
