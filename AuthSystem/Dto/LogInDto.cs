using System.ComponentModel.DataAnnotations;

namespace AuthSystem.Dto
{
    public class LogInDto
    {
        [Required(ErrorMessage = "Password required.")]
        [EmailAddress(ErrorMessage = "Invalid Email.")]
        public string Email { get; set; } = default!;

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(50, MinimumLength = 8, ErrorMessage = "Password ")]
        public string Password { get; set; } = default!;
    }
}
