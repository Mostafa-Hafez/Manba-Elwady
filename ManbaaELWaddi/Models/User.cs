using System.ComponentModel.DataAnnotations;

namespace ManbaaELWaddi.Models
{
    public class User
    {
        [Key]
        public int UserId { get; set; }
        public string? UserName { get; set; }
        public string? Password { get; set; }
        public string? UserPhoneNumber { get; set; }
        public string? CarNumber { get; set; }
        public int? UserQuantityFull { get; set; }
        public int? UserQuantityEmpty { get; set; }
        public int? UserQuantityEmptyWithClients { get; set; }
        public List<Client>? Clients { get; set; } 
        public List<Invoice>? Invoices { get; set; } 
        public List<Order>? Orders { get; set; } 
        public List<CalcQuot>? CalcQuots { get; set; } 

    }
}
