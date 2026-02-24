using AuthSystem.Models;

namespace AuthSystem.Dto
{
    public class UserResponseDto
    {
        public string Id { get; set; } = default!;
        public string UserName { get; set; } = default!;
        public string Email { get; set; } = default!;
        public Roles Role { get; set; }
        public Status Status { get; set; }
        public AccountStatus AccountStatus { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
