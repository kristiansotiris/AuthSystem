using System.ComponentModel.DataAnnotations;

namespace AuthSystem.Dto
{
    public class EmailDto
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = string.Empty;
    }
}
