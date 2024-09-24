namespace ManbaaELWaddi.ViewModels.Invoice
{
    public class GetInvoiceVM
    {
        public int InvoiceId { get; set; }
        public string? InvoiceNumber { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public int? InvoiceQuantity { get; set; }
        public int? FkUserId { get; set; }
        public string? InvoiceImage { get; set; }
    }
}
