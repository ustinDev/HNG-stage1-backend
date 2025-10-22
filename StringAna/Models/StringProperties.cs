using System.Text.Json.Serialization;

namespace StringAnalyzerApi.Models
{
    public class StringProperties
    {
        public int Length { get; set; }
        public bool IsPalindrome { get; set; }
        public int UniqueCharacters { get; set; }
        public int WordCount { get; set; }
        public string Sha256Hash { get; set; } = string.Empty;

        // Use string keys so JSON shows characters as keys
        public Dictionary<string, int> CharacterFrequencyMap { get; set; } = new();
    }
}
