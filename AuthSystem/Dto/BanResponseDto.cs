namespace AuthSystem.Dto
{
    public class BanResponseDto
    {
        public UserResponseDto User { get; set; } = default!;
        public string Message { get; set; } = default!;
    }
}
