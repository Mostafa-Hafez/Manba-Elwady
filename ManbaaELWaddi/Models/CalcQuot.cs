using System.ComponentModel.DataAnnotations;

namespace ManbaaELWaddi.Models
{
    public class CalcQuot
    {
        [Key]
        public int CalcQuotId { get; set; }
        public int? UserQuantityOutForClients { get; set; }
        public int? ClientQuantityOut { get; set; }
        public DateTime? QuantityOutDate { get; set; }
        public int? FkClientId { get; set; } // Foreign Key
        public int? FkUserId { get; set; } // Foreign Key
        public User? FkUser { get; set; } // Navigation Property

    }
}
