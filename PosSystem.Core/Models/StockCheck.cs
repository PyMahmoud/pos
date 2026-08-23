namespace PosSystem.Core.Models
{
    /// <summary>
    /// One "what's on the shelf today" reading from a single pharmacy
    /// visit — quantity, batch/lot, and expiry, per the client's stated
    /// requirement. Append-only by design: never updated in place, only
    /// inserted, so the rep can see how a pharmacy's stock of a given
    /// medication trended visit to visit, not just the latest number.
    /// </summary>
    public class StockCheck
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public string GoodBarcode { get; set; }
        public string MedicationName { get; set; }
        public double Quantity { get; set; }
        public string BatchNumber { get; set; }
        public string ExpiryDate { get; set; }
        public string CheckDate { get; set; }
        public string CheckTime { get; set; }
        public string Notes { get; set; }
    }
}
