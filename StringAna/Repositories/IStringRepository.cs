using StringAnalyzerApi.Models;

namespace StringAnalyzerApi.Repositories
{
    public interface IStringRepository
    {
        void Add(StringRecord record);
        bool Exists(string id);
        StringRecord? Get(string id);
        IEnumerable<StringRecord> GetAll();
        void Delete(string id);
    }
}
