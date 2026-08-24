namespace PosSystem.App.ViewModels
{
    /// <summary>One row in a customer's "what I've sold them" summary — aggregated across every linked Bills row.</summary>
    public class SoldMedicationSummary
    {
        public string Name { get; set; }
        public double TotalQuantity { get; set; }
        public double TotalRevenue { get; set; }
    }
}
