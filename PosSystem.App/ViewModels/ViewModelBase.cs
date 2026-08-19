using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PosSystem.App.ViewModels
{
    /// <summary>
    /// Base class for all ViewModels. Inherit from this for every screen
    /// (CheckoutViewModel, CustomersViewModel, DashboardViewModel, etc.)
    /// instead of putting logic directly in .xaml.cs code-behind.
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
