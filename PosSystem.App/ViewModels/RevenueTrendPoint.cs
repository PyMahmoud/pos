namespace PosSystem.App.ViewModels
{
    /// <summary>
    /// One bucket (day or week, depending on BuildRevenueTrendChart's
    /// aggregation rule) on the Dashboard's Revenue Trend line — carries
    /// Profit alongside Revenue so the hover tooltip can show both, same
    /// reasoning as TopItemPoint.
    /// </summary>
    public class RevenueTrendPoint
    {
        public double Revenue { get; set; }
        public double Profit { get; set; }
    }
}
