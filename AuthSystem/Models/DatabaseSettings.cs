namespace AuthSystem.Models
{
    public class DatabaseSettings
    {
        public string ConnectionString { get; set; } = default!;
        public string DatabaseName { get; set; } = default!;
        public string Collection { get; set; } = default!;
    }
}
