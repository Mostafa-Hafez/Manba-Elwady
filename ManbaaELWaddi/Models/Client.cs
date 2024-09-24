using System.ComponentModel.DataAnnotations;

namespace ManbaaELWaddi.Models
{
    public class Client
    {
        [Key]
        public int ClientId { get; set; }
        public string? ClientName { get; set; }
        public string? ClientNumber { get; set; }
        public int? ClientQuantity { get; set; }
        public string? ClientPhone1 { get; set; }
        public string? ClientPhone2 { get; set; }
        public string? ClientPhone3 { get; set; }
        public string? ClientDistrict { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? AddedBy { get; set; }
        public string? ClientImage { get; set; }
        public int FkUserId { get; set; }
        public User? FkUser { get; set; }



    }
}
