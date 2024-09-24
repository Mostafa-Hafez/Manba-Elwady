using System.ComponentModel.DataAnnotations;

namespace ManbaaELWaddi.ViewModels.Inventory
{
    public class AddInventoryVM
    {
        [Required]
        public int? InventoryQuantityEmpty { get; set; }
    }
}
