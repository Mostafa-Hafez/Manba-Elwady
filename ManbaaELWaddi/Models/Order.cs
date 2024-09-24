using System.ComponentModel.DataAnnotations;

namespace ManbaaELWaddi.Models
{
    public class Order
    {
        [Key]
        public int OrderId { get; set; }
        public string? OrderNumber { get; set; }
        public int? OrderQuantity { get; set; }
        public DateTime OrderDate { get; set; }
        public int FkUserId { get; set; } // Foreign Key
        public User? FkUser { get; set; } // Navigation Property
        public int FkClientId { get; set; }
        public Client? FkClient { get; set; }
    }
}
