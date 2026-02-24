using AuthSystem.Models;
namespace AuthSystem.Dto
{
    public class SearchDto
    {
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public Roles Role { get; set; }
        public AccountStatus AccountStatus { get; set; }
        public Status Status { get; set; }
    }
}
