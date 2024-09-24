using System.ComponentModel.DataAnnotations;

namespace ManbaaELWaddi.ViewModels.Order
{
    public class AddOrderVM
    {
        [Required]
        public string? OrderQuantity { get; set; }
        public int FkUserId { get; set; }
        public int FkClientId { get; set; }

    }
}
