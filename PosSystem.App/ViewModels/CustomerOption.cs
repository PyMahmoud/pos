namespace PosSystem.App.ViewModels
{
    /// <summary>
    /// One entry in Checkout's customer picker. Same null-means-"none"
    /// pattern as CategoryChip's Value: Model == null is the "Walk-in"
    /// entry, always first in the list, and is what CheckoutViewModel.
    /// CanPayLater checks to keep Pay Later disabled until a real customer
    /// is selected.
    /// </summary>
    public class CustomerOption
    {
        public string DisplayName { get; set; }
        public Core.Models.Customers Model { get; set; }
    }
}
