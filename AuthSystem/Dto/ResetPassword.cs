using System.ComponentModel.DataAnnotations;

namespace AuthSystem.Dto
{
    public class ResetPassword
    {
        [Required(ErrorMessage = "Password is required.")]
        [StringLength(50, MinimumLength = 8, ErrorMessage = "Password must containe at least 8 characters")]
        public string Password { get; set; } = default!;

        [Required(ErrorMessage = "Confirm Password is required !")]
        [StringLength(50, MinimumLength = 8, ErrorMessage = "Confirm Password must be at least 8 characters")]
        public string ConfirmPassword { get; set; } = default!;
    }
}
