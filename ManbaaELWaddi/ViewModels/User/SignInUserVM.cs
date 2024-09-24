using System.ComponentModel.DataAnnotations;

namespace ManbaaELWaddi.ViewModels.User
{
    public class SignInUserVM
    {
        [Required]
        public string? UserName { get; set; }
        [Required]
        public string?  Password { get; set; }
    }
}
