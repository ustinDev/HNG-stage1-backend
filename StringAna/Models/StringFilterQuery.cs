namespace StringAnalyzerApi.Models
{
    // For query binding from /strings?...
    public class StringFilterQuery
    {
        // Query parameters
        public bool? IsPalindrome { get; set; }
        public int? MinLength { get; set; }
        public int? MaxLength { get; set; }
        public int? WordCount { get; set; }
        public string? ContainsCharacter { get; set; }
    }
}
