using System.ComponentModel.DataAnnotations;

namespace ManbaaELWaddi.ViewModels.Invoice
{
    public class AddInvoiceVM
    {
        public int? FkUserId { get; set; }
        [Required]
        public int? InvoiceQuantity { get; set; }
        [Required]
        public string? InvoiceNumber { get; set; }
        [Required]
        public IFormFile? InvoiceImage { get; set; }
    }
}
