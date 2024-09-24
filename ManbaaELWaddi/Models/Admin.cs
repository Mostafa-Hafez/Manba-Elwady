using System.ComponentModel.DataAnnotations;

namespace ManbaaELWaddi.Models
{
    public class Admin
    {
        [Key]
        public int AdminID { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        

    }
}
