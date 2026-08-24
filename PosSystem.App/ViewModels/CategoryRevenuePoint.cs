namespace PosSystem.App.ViewModels
{
    /// <summary>
    /// One bar in the Dashboard's "Revenue by Category" chart — carries
    /// Quantity (units sold) and Profit alongside Revenue so the hover
    /// tooltip can show all three, same reasoning as TopItemPoint. Category
    /// is kept even though the axis label already shows it, so the tooltip
    /// formatter doesn't have to reach back into the axis Labels array to
    /// say which bar it's on.
    /// </summary>
    public class CategoryRevenuePoint
    {
        public string Category { get; set; }
        public double Quantity { get; set; }
        public double Revenue { get; set; }
        public double Profit { get; set; }
    }
}
