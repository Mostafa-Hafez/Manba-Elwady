using System.ComponentModel.DataAnnotations;

namespace ManbaaELWaddi.Models
{
    public class District
    {
        [Key]
        public int DistrictId { get; set; }

        [Required]
        public string? DistrictName { get; set; }

        [Required]
        public string? City { get; set; }
    }
}
