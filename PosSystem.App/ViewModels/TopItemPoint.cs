namespace PosSystem.App.ViewModels
{
    /// <summary>
    /// One bar in the Dashboard's "Top Items" chart — all-time, unfiltered
    /// (see BuildTopItemsChart's comment in DashboardViewModel.cs). Carries
    /// Quantity (the bar height), Revenue, and Profit, so the hover tooltip
    /// can show all three instead of just the one the chart is plotted on.
    /// </summary>
    public class TopItemPoint
    {
        public string Name { get; set; }
        public double Quantity { get; set; }
        public double Revenue { get; set; }
        public double Profit { get; set; }
    }
}
