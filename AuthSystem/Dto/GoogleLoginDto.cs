namespace AuthSystem.Dto
{
    public class GoogleLoginDto
    {
        public string Email { get; set; } = string.Empty;
        public string? UserName { get; set; }
        public string? GoogleId { get; set; }
        public string? ProfilePicture { get; set; }
    }
}
