namespace ManbaaELWaddi.ViewModels.User
{
    public class GetUserVM
    {
        public int UserId { get; set; }
        public string? UserName { get; set; }
        public string? Password { get; set; }
        public string? UserPhoneNumber { get; set; }
        public string? CarNumber { get; set; }
        public int? UserQuantityFull { get; set; }
        public int? UserQuantityEmpty { get; set; }
        public int? UserQuantityEmptyWithClients { get; set; }

    }
}
