namespace ManbaaELWaddi.ViewModels.Client
{
    public class ClientMonthlyOrderSummaryVM
    {
        public int ClientId { get; set; }
        public string? ClientName { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public int Period { get; set; }
        public int TotalQuantity { get; set; }
        public string? CarNumber { get; set; }


    }

}
