using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PosSystem.App.ViewModels
{
    /// <summary>
    /// One row on the Customers screen. Wraps a Core.Models.Customers with
    /// INotifyPropertyChanged so Paid/Remain repaint immediately after
    /// CustomersViewModel reloads the list. PaymentInput is this row's own
    /// payment-amount TextBox binding — kept per-row (not one shared field
    /// on the ViewModel) so typing an amount into one customer's box can
    /// never bleed into another's.
    /// </summary>
    public class CustomerRow : INotifyPropertyChanged
    {
        public int Id { get; }
        public string Ownername { get; }
        public string Ownerid { get; }
        public string Ownernumber { get; }

        private double _paid;
        public double Paid
        {
            get => _paid;
            set { if (_paid == value) return; _paid = value; OnPropertyChanged(); }
        }

        private double _remain;
        public double Remain
        {
            get => _remain;
            set
            {
                if (_remain == value) return;
                _remain = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasDebt));
            }
        }

        public bool HasDebt => Remain > 0;

        // Added for "money we owe the customer" (2026-08-31) -- the flip
        // side of Remain/HasDebt above. See DatabaseBootstrapper's
        // customers.CreditOwed comment for how this can grow.
        private double _creditOwed;
        public double CreditOwed
        {
            get => _creditOwed;
            set
            {
                if (_creditOwed == value) return;
                _creditOwed = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasCredit));
            }
        }

        public bool HasCredit => CreditOwed > 0;

        private string _paymentInput = "";
        public string PaymentInput
        {
            get => _paymentInput;
            set { if (_paymentInput == value) return; _paymentInput = value; OnPropertyChanged(); }
        }

        public CustomerRow(Core.Models.Customers model)
        {
            Id = model.Id;
            Ownername = model.Ownername;
            Ownerid = model.Ownerid;
            Ownernumber = model.Ownernumber;
            _paid = model.Paid;
            _remain = model.Remain;
            _creditOwed = model.CreditOwed;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
