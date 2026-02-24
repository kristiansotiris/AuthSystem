using System.ComponentModel.DataAnnotations;

namespace AuthSystem.Dto
{
    public class RegisterUserDto
    {
        [Required(ErrorMessage = "User Name is required.")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "User Name must be 3-50 characters")]
        public string UserName { get; set; } = default!;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid Email.")]
        public string Email { get; set; } = default!;

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must at lease be 8 characters.")]
        public string PasswordHash { get; set; } = default!;

    }
}
