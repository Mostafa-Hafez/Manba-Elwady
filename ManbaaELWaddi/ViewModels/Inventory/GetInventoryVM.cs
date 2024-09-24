namespace ManbaaELWaddi.ViewModels.Inventory
{
    public class GetInventoryVM
    {
        public int InventoryId { get; set; }
        public string? InventoryName { get; set; }
        public int? InventoryQuantityFull { get; set; }
        public int? InventoryQuantityEmpty { get; set; }
        public int? InventoryQuantityEmptyWithUsers { get; set; }
        public string? Description { get; set; }
        public string? AddedBy { get; set; }
        public int? AddedById { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
