namespace ElWadManbaaELWaddidi.Models
{
    public class Inventory
    {
        public int InventoryId { get; set; }
        public int? InventoryQuantityFull { get; set; }
        public int? InventoryQuantityEmpty { get; set; }
        public int? InventoryQuantityEmptyWithUsers { get; set; }
        public int? InventoryQuantityEmptyWithClients { get; set; }
        public int? AllScrap { get; set; }
        public string? AddedBy { get; set; }
        public int? AddedById { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

    }
}
