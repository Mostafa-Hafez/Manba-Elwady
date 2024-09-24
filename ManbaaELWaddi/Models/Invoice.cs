using System.ComponentModel.DataAnnotations;

namespace ManbaaELWaddi.Models
{
    public class Invoice
    {
        [Key]
        public int InvoiceId { get; set; }
        public string? InvoiceNumber { get; set; }
        public int? InvoiceQuantity { get; set; }
        public DateTime InvoiceDate { get; set; }
        public string? InvoiceImage { get; set; }
        public int? FkUserId { get; set; }
        public User? FkUser { get; set; }


    }
}
