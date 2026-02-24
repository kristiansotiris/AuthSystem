using AuthSystem.Models;

namespace AuthSystem.Dto
{
    public class UpdateUserDto
    {
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public Status Status { get; set; }
    }
}
