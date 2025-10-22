namespace StringAnalyzerApi.Models
{
    public class StringRecord
    {
        public string Id { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public StringProperties Properties { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }
}
