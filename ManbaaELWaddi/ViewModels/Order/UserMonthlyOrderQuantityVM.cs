namespace ManbaaELWaddi.ViewModels.Order
{
    public class UserMonthlyOrderQuantityVM
    {
        public int UserId { get; set; }
        public string? UserName { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public int TotalOrderQuantity { get; set; }
    }
}
