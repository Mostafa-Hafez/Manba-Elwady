using System.ComponentModel.DataAnnotations;

namespace ManbaaELWaddi.ViewModels.Client
{
    public class AddClientVM
    {
        [Required]
        public string? ClientName { get; set; }
        [Required]
        public string? ClientPhone1 { get; set; }
        public string? ClientPhone2 { get; set; }
        public string? ClientPhone3 { get; set; }
        [Required]
        public string? ClientDistrict { get; set; }
        public string? Description { get; set; }
        public IFormFile? ClientImage { get; set; }
        public int? FkUserId { get; set; }
        public int? InitialQuantity { get; set; }


    }
}
