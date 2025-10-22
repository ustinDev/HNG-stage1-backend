using StringAnalyzerApi.Models;
using System.Collections.Concurrent;

namespace StringAnalyzerApi.Repositories
{
    // Simple thread-safe in-memory store
    public class InMemoryStringRepository : IStringRepository
    {
        private readonly ConcurrentDictionary<string, StringRecord> _store = new();

        public void Add(StringRecord record)
        {
            _store[record.Id] = record;
        }

        public bool Exists(string id) => _store.ContainsKey(id);

        public StringRecord? Get(string id)
        {
            _store.TryGetValue(id, out var rec);
            return rec;
        }

        public IEnumerable<StringRecord> GetAll() => _store.Values.OrderBy(r => r.CreatedAt);

        public void Delete(string id)
        {
            _store.TryRemove(id, out _);
        }
    }
}
