namespace AuthSystem.Dto
{
    public class VerificationResponse
    {
        public string? Message { get; set; }
        public string? VerificationCode { get; set; }
        public string? VerificationToken { get; set; }
        public int StatusCode { get; set; } = 200;
        public string? Url { get; set; }
    }
}
