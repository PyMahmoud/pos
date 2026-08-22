namespace PosSystem.App.ViewModels
{
    /// <summary>
    /// One chip in Checkout's category filter row. Value is the raw
    /// category string used for filtering (null = "all items", always the
    /// first chip); DisplayName is what's shown, and gets re-localized when
    /// CheckoutViewModel rebuilds the chip list on a language change — kept
    /// as a separate field so the filter itself never depends on which
    /// language is active.
    /// </summary>
    public class CategoryChip
    {
        public string DisplayName { get; set; }
        public string Value { get; set; }
    }
}
