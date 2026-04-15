using Azure.Identity;
using Microsoft.Azure.Cosmos;
using StackExchange.Redis;

namespace Provider.Interfaces
{
    public class AzureRedisCacheProvider : IAzureRedisCacheProvider, IDisposable
    //: IDisposable
    {
        private readonly ConnectionMultiplexer _connection;
        private readonly IDatabase _database;
        private readonly string cacheHostName;
        private readonly int connectionPoolSize = 16;
        public AzureRedisCacheProvider(string cacheHostName, int connectionPoolSize = 16)
        {

            this.cacheHostName = cacheHostName;
            this.connectionPoolSize = connectionPoolSize;
            var configurationOptions = ConfigurationOptions.Parse($"{cacheHostName}:6380");
#if DEBUG
            var credential = new DefaultAzureCredential();
#else
            var credential = new ManagedIdentityCredential();
#endif
            configurationOptions.ConfigureForAzureWithTokenCredentialAsync(credential).Wait();
            configurationOptions.ConnectTimeout = 300000;
            configurationOptions.KeepAlive = 180;
            configurationOptions.AbortOnConnectFail = false;
            configurationOptions.ReconnectRetryPolicy = new ExponentialRetry(1000);
            configurationOptions.SyncTimeout = 300000;
            if (connectionPoolSize != 16)
            {
                configurationOptions.SocketManager = new SocketManager("MySockets", connectionPoolSize);
            }
            _connection = ConnectionMultiplexer.Connect(configurationOptions);
            _database = _connection.GetDatabase();

        }

        // String operations
        public async Task SetAsync(string key, string value, TimeSpan? ttl = null)
        {

            if (ttl.HasValue)
            {
                await _database.StringSetAsync(key, value, ttl.Value);
            }
            else
            {
                await _database.StringSetAsync(key, value);
            }
        }

        public async Task<string> GetAsync(string key)
        {
            return (await _database.StringGetAsync(key))!;
        }

        // Batch operations
        public async Task BatchSetAsync(Dictionary<string, string> keyValuePairs, int batchSize, TimeSpan? ttl = null)
        {

            var tasks = new List<Task>();
            var batch = _database.CreateBatch();
            int count = 0;

            foreach (var kvp in keyValuePairs)
            {
                if (ttl.HasValue)
                {
                    tasks.Add(batch.StringSetAsync(kvp.Key, kvp.Value, ttl.Value, flags: CommandFlags.DemandMaster));
                }
                else
                {
                    tasks.Add(batch.StringSetAsync(kvp.Key, kvp.Value, flags: CommandFlags.DemandMaster));
                }
                count++;
                if (count % batchSize == 0)
                {
                    batch.Execute();
                    await Task.WhenAll(tasks);
                    tasks.Clear();
                    batch = _database.CreateBatch();
                }
            }

            // Execute any remaining tasks
            if (tasks.Count > 0)
            {
                batch.Execute();
                await Task.WhenAll(tasks);
            }
        }



        public IEnumerable<RedisKey> GetKeysByPrefix(string prefix)
        {
            var keys = new List<RedisKey>();
            var endpoints = _connection.GetEndPoints();
            foreach (var endpoint in endpoints)
            {
                var server = _connection.GetServer(endpoint);
                var cursor = 0UL;
                var batchSize = 60000; // Adjust this value based on your needs
                do
                {
                    var result = server.Execute("SCAN", cursor.ToString(), "MATCH", $"{prefix}:*", "COUNT", batchSize.ToString());
                    var resultArray = (RedisResult[])result!;
                    cursor = Convert.ToUInt64((string)resultArray[0]!);
                    var fetchedKeys = ((RedisResult[])resultArray[1]!).Select(k => (RedisKey)(string)k!).ToList();

                    if (fetchedKeys == null || !fetchedKeys.Any())
                    {
                        continue;
                    }

                    keys.AddRange(fetchedKeys);
                } while (cursor != 0UL);
            }
            return keys;
        }




        public async Task<Dictionary<string,TimeSpan>> BatchGetTTLAsync(HashSet<string> keys, int batchSize)
        {
            var result = keys.ToDictionary(k => k, k =>  default(TimeSpan));
            var redisKeys = keys.Select(x => new RedisKey(x)).ToArray();
            int totalBatches = (int)Math.Ceiling((double)redisKeys.Length / batchSize);
            for (int batchIndex = 0; batchIndex < totalBatches; batchIndex++)
            {
                var batch = redisKeys.Skip(batchIndex * batchSize).Take(batchSize).ToArray();
                var  redisResult = await Task.WhenAll(batch.Select(key => _database.KeyTimeToLiveAsync(key)));
                for (int i = 0; i < batch.Length; i++)
                {
                    var key = batch[i];
                    result[key!] = redisResult[i] ?? default(TimeSpan); // Use default TTL if the TTL is not found
                }
            }
            return result;
        }

        public async Task<Dictionary<string, bool>> BatchSetTTLAsync(HashSet<string> keys,long timeInMinutes, int batchSize)
        {
            var result = keys.ToDictionary(k => k, k => default(bool));
            var redisKeys = keys.Select(x => new RedisKey(x)).ToArray();
            int totalBatches = (int)Math.Ceiling((double)redisKeys.Length / batchSize);
            for (int batchIndex = 0; batchIndex < totalBatches; batchIndex++)
            {
                var batch = redisKeys.Skip(batchIndex * batchSize).Take(batchSize).ToArray();
                var redisResult = await Task.WhenAll(batch.Select(key => _database.KeyExpireAsync(key, TimeSpan.FromMinutes(timeInMinutes))));
                for (int i = 0; i < batch.Length; i++)
                {
                    var key = batch[i];
                    result[key!] = redisResult[i]; // Use default TTL if the TTL is not found
                }
            }
            return result;
        }


        public async Task<Dictionary<string, string>> BatchGetAsync(HashSet<string> keys, int batchSize)
        {
            var result = keys.ToDictionary(k => k, k => (string)null!);
            var redisKeys = keys.Select(x => new RedisKey(x)).ToArray();
            int totalBatches = (int)Math.Ceiling((double)redisKeys.Length / batchSize);

            for (int batchIndex = 0; batchIndex < totalBatches; batchIndex++)
            {
                var batch = redisKeys.Skip(batchIndex * batchSize).Take(batchSize).ToArray();
                RedisValue[] redisResult = await Task.WhenAll(batch.Select(key => _database.StringGetAsync(key)));
                //var redisResult = await _database.StringGetAsync(batch);

                for (int i = 0; i < batch.Length; i++)
                {
                    var key = batch[i];
                    if (redisResult[i].IsNull)
                    {
                        result[key!] = null!; // Handle missing keys
                    }
                    else
                    {
                        result[key!] = redisResult[i]!; // Use default TTL if the TTL is not found
                    }
                }
            }

            return result;
        }

        public async Task ListAddAsync(string key, string value)
        {
            await _database.ListRightPushAsync(key, value);
        }

        public async Task<string[]> ListGetAsync(string key)
        {
            return ((await _database.ListRangeAsync(key)).ToStringArray())!;
        }

        // Set operations
        public async Task SetAddAsync(string key, string value)
        {
            await _database.SetAddAsync(key, value);
        }

        public async Task<bool> SetContainsAsync(string key, string value)
        {
            return await _database.SetContainsAsync(key, value);
        }

        // Hash operations
        public async Task HashSetAsync(string key, string field, string value)
        {
            await _database.HashSetAsync(key, field, value);
        }

        public async Task<string> HashGetAsync(string key, string field)
        {
            return (await _database.HashGetAsync(key, field))!;
        }

        // Sorted Set operations
        public async Task SortedSetAddAsync(string key, string value, double score)
        {
            await _database.SortedSetAddAsync(key, value, score);
        }

        public async Task<SortedSetEntry[]> SortedSetRangeByScoreAsync(string key, double start, double stop)
        {
            return await _database.SortedSetRangeByScoreWithScoresAsync(key, start, stop);
        }

        // Stream operations
        public async Task StreamAddAsync(string key, string field, string value)
        {
            await _database.StreamAddAsync(key, field, value);
        }

        public async Task<StreamEntry[]> StreamRangeAsync(string key, string start, string end)
        {
            return await _database.StreamRangeAsync(key, start, end);
        }

        // Bitmap operations
        public async Task SetBitAsync(string key, long offset, bool bit)
        {
            await _database.StringSetBitAsync(key, offset, bit);
        }

        public async Task<bool> GetBitAsync(string key, long offset)
        {
            return await _database.StringGetBitAsync(key, offset);
        }


        // HyperLogLog operations
        public async Task HyperLogLogAddAsync(string key, string value)
        {
            await _database.HyperLogLogAddAsync(key, value);
        }

        public async Task<long> HyperLogLogCountAsync(string key)
        {
            return await _database.HyperLogLogLengthAsync(key);
        }

        // Geospatial operations
        public async Task GeoAddAsync(string key, double longitude, double latitude, string member)
        {
            await _database.GeoAddAsync(key, longitude, latitude, member);
        }

        public async Task<GeoPosition?> GeoPositionAsync(string key, string member)
        {
            return await _database.GeoPositionAsync(key, member);
        }

        // JSON operations (via RedisJSON module)
        public async Task JsonSetAsync(string key, string path, string json)
        {
            await _database.ExecuteAsync("JSON.SET", key, path, json);
        }

        public async Task<string> JsonGetAsync(string key, string path)
        {
            return ((string)await _database.ExecuteAsync("JSON.GET", key, path))!;
        }

        // Bloom filter operations (via RedisBloom module)
        public async Task BloomFilterAddAsync(string key, string value)
        {
            await _database.ExecuteAsync("BF.ADD", key, value);
        }

        public async Task<bool> BloomFilterExistsAsync(string key, string value)
        {
            return (bool)await _database.ExecuteAsync("BF.EXISTS", key, value);
        }


        public void Dispose()
        {
            _connection?.Dispose();
        }

    }
}
