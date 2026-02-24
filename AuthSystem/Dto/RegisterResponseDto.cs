namespace AuthSystem.Dto
{
    public class RegisterResponseDto
    {
        public UserResponseDto User { get; set; } = default!;
        public string Message { get; set; } = default!;

    }
}
