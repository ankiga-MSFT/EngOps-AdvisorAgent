using StackExchange.Redis;

namespace Provider.Interfaces
{
    public interface IAzureRedisCacheProvider
    {
        Task<Dictionary<string, TimeSpan>> BatchGetTTLAsync(HashSet<string> keys, int batchSize);
        Task<Dictionary<string, bool>> BatchSetTTLAsync(HashSet<string> keys, long timeInMinutes, int batchSize);
        Task<Dictionary<string, string>> BatchGetAsync(HashSet<string> keys, int batchSize);
        IEnumerable<RedisKey> GetKeysByPrefix(string prefix);
        Task BatchSetAsync(Dictionary<string, string> keyValuePairs,  int batchSize, TimeSpan? ttl = null);
        Task BloomFilterAddAsync(string key, string value);
        Task<bool> BloomFilterExistsAsync(string key, string value);
        Task GeoAddAsync(string key, double longitude, double latitude, string member);
        Task<GeoPosition?> GeoPositionAsync(string key, string member);
        Task<string> GetAsync(string key);
        Task<bool> GetBitAsync(string key, long offset);
        Task<string> HashGetAsync(string key, string field);
        Task HashSetAsync(string key, string field, string value);
        Task HyperLogLogAddAsync(string key, string value);
        Task<long> HyperLogLogCountAsync(string key);
        Task<string> JsonGetAsync(string key, string path);
        Task JsonSetAsync(string key, string path, string json);
        Task ListAddAsync(string key, string value);
        Task<string[]> ListGetAsync(string key);
        Task SetAddAsync(string key, string value);
        Task SetAsync(string key,  string value, TimeSpan? ttl = null);
        Task SetBitAsync(string key, long offset, bool bit);
        Task<bool> SetContainsAsync(string key, string value);
        Task SortedSetAddAsync(string key, string value, double score);
        Task<SortedSetEntry[]> SortedSetRangeByScoreAsync(string key, double start, double stop);
        Task StreamAddAsync(string key, string field, string value);
        Task<StreamEntry[]> StreamRangeAsync(string key, string start, string end);
    }
}