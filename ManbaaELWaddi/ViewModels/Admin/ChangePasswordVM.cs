using System.ComponentModel.DataAnnotations;

namespace ManbaaELWaddi.ViewModels.Admin
{
    public class ChangePasswordVM
    {
        [Required]
        public int UserId { get; set; }  // The ID of the user whose password is being changed

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
        public string? NewPassword { get; set; }  // The new password for the user

        [Required]
        [Compare("NewPassword", ErrorMessage = "The password and confirmation password do not match.")]
        public string? ConfirmPassword { get; set; }  // Confirmation of the new password
    }
}
