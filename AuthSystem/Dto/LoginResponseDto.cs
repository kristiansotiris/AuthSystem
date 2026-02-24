namespace AuthSystem.Dto
{
    public class LoginResponseDto
    {
        public UserResponseDto User { get; set; } = default!;
        //public string Token { get; set; } = default!;
        public string Message { get; set; } = default!;
    }
}
