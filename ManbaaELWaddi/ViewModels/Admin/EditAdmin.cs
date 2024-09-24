using System.ComponentModel.DataAnnotations;

namespace ManbaaELWaddi.ViewModels.Admin
{
    public class EditAdmin
    {
        [Required]
        public string? NewUsername { get; set; }
        [Required]
        public string? NewPassword { get; set; }
    }
}

