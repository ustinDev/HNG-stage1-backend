using Microsoft.AspNetCore.Mvc;
using StringAnalyzerApi.Models;
using StringAnalyzerApi.Repositories;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StringAnalyzerApi.Controllers
{
    [ApiController]
    [Route("strings")]
    public class StringsController : ControllerBase
    {
        private readonly IStringRepository _repo;

        public StringsController(IStringRepository repo)
        {
            _repo = repo;
        }

        // 1. Create / Analyze
        [HttpPost]
        public ActionResult<StringRecord> Create([FromBody] CreateStringRequest request)
        {
            if (request is null)
                return BadRequest(new { error = "Request body is required." });

            if (request.Value is null)
                return UnprocessableEntity(new { error = "\"value\" field must be a string." });

            if (!(request.Value is string))
                return UnprocessableEntity(new { error = "\"value\" must be a string." });

            var value = request.Value!;
            var sha = ComputeSha256Hash(value);

            if (_repo.Exists(sha))
                return Conflict(new { error = "String already exists.", id = sha });

            var props = AnalyzeString(value, sha);
            var record = new StringRecord
            {
                Id = sha,
                Value = value,
                Properties = props,
                CreatedAt = DateTime.UtcNow
            };

            _repo.Add(record);
            return CreatedAtAction(nameof(GetByValue), new { string_value = value }, record);
        }

        // 2. Get Specific
        [HttpGet("{string_value}")]
        public ActionResult<StringRecord> GetByValue([FromRoute] string string_value)
        {
            // Allow URL-encoded strings
            if (string_value is null)
                return BadRequest();

            // compute sha of the provided string to look up
            var sha = ComputeSha256Hash(string_value);
            var rec = _repo.Get(sha);
            if (rec == null)
                return NotFound(new { error = "String not found." });

            return Ok(rec);
        }

        // 3. Get All with Filtering
        [HttpGet]
        public ActionResult GetAll([FromQuery] StringFilterQuery query)
        {
            // Validate query params types (model binding already handles types; here we ensure values are sane)
            if (query.MinLength.HasValue && query.MinLength < 0) return BadRequest(new { error = "min_length must be >= 0" });
            if (query.MaxLength.HasValue && query.MaxLength < 0) return BadRequest(new { error = "max_length must be >= 0" });
            if (query.MinLength.HasValue && query.MaxLength.HasValue && query.MinLength > query.MaxLength)
                return BadRequest(new { error = "min_length cannot be greater than max_length" });

            var all = _repo.GetAll();

            var filtered = all.Where(r =>
            {
                var p = r.Properties;
                if (query.IsPalindrome.HasValue && p.IsPalindrome != query.IsPalindrome.Value) return false;
                if (query.MinLength.HasValue && p.Length < query.MinLength.Value) return false;
                if (query.MaxLength.HasValue && p.Length > query.MaxLength.Value) return false;
                if (query.WordCount.HasValue && p.WordCount != query.WordCount.Value) return false;
                if (!string.IsNullOrEmpty(query.ContainsCharacter))
                {
                    if (query.ContainsCharacter.Length != 1) return false; // contains_character should be single char
                    var ch = query.ContainsCharacter[0];
                    if (!p.CharacterFrequencyMap.ContainsKey(ch.ToString()))
                        return false;
                }
                return true;
            }).ToArray();

            var response = new
            {
                data = filtered,
                count = filtered.Length,
                filters_applied = new
                {
                    is_palindrome = query.IsPalindrome,
                    min_length = query.MinLength,
                    max_length = query.MaxLength,
                    word_count = query.WordCount,
                    contains_character = query.ContainsCharacter
                }
            };

            return Ok(response);
        }

        // 4. Natural language filtering
        [HttpGet("filter-by-natural-language")]
        public ActionResult NaturalLanguageFilter([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return BadRequest(new { error = "query parameter is required." });

            var parseResult = NaturalLanguageParser.Parse(query);

            if (!parseResult.Success)
                return BadRequest(new { error = "Unable to parse natural language query." });

            // check for conflicting filters (very simple: none at the moment)
            if (parseResult.Conflicting)
                return UnprocessableEntity(new { error = "Parsed query resulted in conflicting filters." });

            // Now apply parsed filters
            var all = _repo.GetAll();
            var filtered = all.Where(r =>
            {
                var p = r.Properties;
                if (parseResult.Filters.IsPalindrome.HasValue && p.IsPalindrome != parseResult.Filters.IsPalindrome.Value) return false;
                if (parseResult.Filters.MinLength.HasValue && p.Length < parseResult.Filters.MinLength.Value) return false;
                if (parseResult.Filters.MaxLength.HasValue && p.Length > parseResult.Filters.MaxLength.Value) return false;
                if (parseResult.Filters.WordCount.HasValue && p.WordCount != parseResult.Filters.WordCount.Value) return false;
                if (!string.IsNullOrEmpty(parseResult.Filters.ContainsCharacter))
                {
                    var ch = parseResult.Filters.ContainsCharacter[0];
                    if (!p.CharacterFrequencyMap.ContainsKey(ch.ToString()))
                        return false;
                }
                return true;
            }).ToArray();

            var resp = new
            {
                data = filtered,
                count = filtered.Length,
                interpreted_query = new
                {
                    original = query,
                    parsed_filters = new
                    {
                        word_count = parseResult.Filters.WordCount,
                        is_palindrome = parseResult.Filters.IsPalindrome,
                        min_length = parseResult.Filters.MinLength,
                        max_length = parseResult.Filters.MaxLength,
                        contains_character = parseResult.Filters.ContainsCharacter
                    }
                }
            };

            return Ok(resp);
        }

        // 5. Delete String
        [HttpDelete("{string_value}")]
        public IActionResult Delete([FromRoute] string string_value)
        {
            var sha = ComputeSha256Hash(string_value);
            if (!_repo.Exists(sha))
                return NotFound(new { error = "String not found." });

            _repo.Delete(sha);
            return NoContent();
        }

        #region Helpers

        private static StringProperties AnalyzeString(string value, string sha)
        {
            var length = value.Length;
            var normalized = value.ToLowerInvariant();
            var isPalindrome = IsPalindrome(normalized);
            var uniqueCharacters = normalized.Distinct().Count();
            var wordCount = CountWords(value);
            var freq = CharacterFrequencyMap(value);

            var props = new StringProperties
            {
                Length = length,
                IsPalindrome = isPalindrome,
                UniqueCharacters = uniqueCharacters,
                WordCount = wordCount,
                Sha256Hash = sha,
                CharacterFrequencyMap = freq
            };
            return props;
        }

        private static bool IsPalindrome(string s)
        {
            // define palindrome ignoring case and whitespace
            var cleaned = new string(s.Where(c => !char.IsWhiteSpace(c)).ToArray());
            int i = 0, j = cleaned.Length - 1;
            while (i < j)
            {
                if (cleaned[i] != cleaned[j]) return false;
                i++; j--;
            }
            return true;
        }

        private static int CountWords(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0;
            // split by whitespace
            var parts = s.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length;
        }

        private static Dictionary<string, int> CharacterFrequencyMap(string s)
        {
            var dict = new Dictionary<string, int>();
            foreach (var ch in s)
            {
                var key = ch.ToString();
                if (!dict.ContainsKey(key)) dict[key] = 0;
                dict[key]++;
            }
            return dict;
        }

        private static string ComputeSha256Hash(string raw)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(raw);
            var hash = sha256.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        #endregion
    }

    // Very small natural language parser used by the controller.
    internal static class NaturalLanguageParser
    {
        public class ParseResult
        {
            public bool Success { get; set; }
            public bool Conflicting { get; set; }
            public StringFilterQuery Filters { get; set; } = new StringFilterQuery();
        }

        public static ParseResult Parse(string input)
        {
            var result = new ParseResult { Success = false, Conflicting = false, Filters = new StringFilterQuery() };
            var lowered = input.Trim().ToLowerInvariant();

            // Basic patterns:

            // "single word" or "one word"
            if (lowered.Contains("single word") || lowered.Contains("one word"))
                result.Filters.WordCount = 1;

            // palindromic / palindrome / palindromic strings
            if (lowered.Contains("palindrom") || lowered.Contains("palindrome"))
                result.Filters.IsPalindrome = true;

            // strings longer than N characters -> min_length = N+1
            var longerMatch = System.Text.RegularExpressions.Regex.Match(lowered, @"longer than (\d+)");
            if (longerMatch.Success && int.TryParse(longerMatch.Groups[1].Value, out var n))
            {
                result.Filters.MinLength = n + 1;
            }

            // strings shorter than N characters -> max_length = N-1
            var shorterMatch = System.Text.RegularExpressions.Regex.Match(lowered, @"shorter than (\d+)");
            if (shorterMatch.Success && int.TryParse(shorterMatch.Groups[1].Value, out var m))
            {
                if (m > 0) result.Filters.MaxLength = m - 1;
            }

            // "containing the letter z" or "contains the letter z"
            var letterMatch = System.Text.RegularExpressions.Regex.Match(lowered, @"letter ([a-z])");
            if (letterMatch.Success)
            {
                result.Filters.ContainsCharacter = letterMatch.Groups[1].Value;
            }

            // "containing the letter z" alt pattern: "containing the letter z"
            var containsChar = System.Text.RegularExpressions.Regex.Match(lowered, @"contain(?:s|ing)?(?: the)? ([a-z])\b");
            if (containsChar.Success)
            {
                result.Filters.ContainsCharacter = containsChar.Groups[1].Value;
            }

            // "contain the first vowel" or "that contain the first vowel" -> map to 'a'
            if (lowered.Contains("first vowel"))
                result.Filters.ContainsCharacter = "a";

            // if nothing was recognized, return failure
            var anyFound = result.Filters.IsPalindrome.HasValue
                           || result.Filters.WordCount.HasValue
                           || result.Filters.MinLength.HasValue
                           || result.Filters.MaxLength.HasValue
                           || !string.IsNullOrEmpty(result.Filters.ContainsCharacter);

            result.Success = anyFound;
            return result;
        }
    }
}
