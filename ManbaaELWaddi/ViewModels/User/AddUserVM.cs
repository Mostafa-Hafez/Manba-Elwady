using System.ComponentModel.DataAnnotations;

namespace ManbaaELWaddi.ViewModels.User
{
    public class AddUserVM
    {
        [Required]
        public string? UserName { get; set; }
        [Required]
        public string? Password { get; set; }
        [Required]
        public string? UserPhoneNumber { get; set; }
        [Required]
        public string? CarNumber { get; set; }
        [Required]
        public int? UserQuantityEmpty { get; set; }

    }
}
