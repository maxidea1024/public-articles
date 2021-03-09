using StackExchange.Redis;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace P2TestApp.Cache
{
    public class CacheClient : ICacheClient
    {
        private const int DatabaseIndex = 15;
        private static readonly string HASH_SPLITTER = "%$#&^";

        private readonly TimeSpan DefaultExpiry = TimeSpan.FromSeconds(60);

        private readonly ConfigurationOptions _configuration = null;
        private Lazy<IConnectionMultiplexer> _connection = null;

        public CacheClient(string host = "localhost", int port = 6379, bool allowAdmin = false)
        {
            _configuration = new ConfigurationOptions()
            {
                //for the redis pool so you can extent later if needed
                EndPoints = { { host, port }, },
                AllowAdmin = allowAdmin,
                //Password = "", //to the security for the production
                ClientName = "My Redis Client",
                ReconnectRetryPolicy = new LinearRetry(5000),
                AbortOnConnectFail = false,
            };

            _connection = new Lazy<IConnectionMultiplexer>(() =>
            {
                ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(_configuration);

                redis.GetServer(redis.GetEndPoints().Single())
                    .ConfigSet("notify-keyspace-events", "KEA"); // KEA=everything

                redis.ErrorMessage += _Connection_ErrorMessage;

                //redis.InternalError += _Connection_InternalError;
                //redis.ConnectionFailed += _Connection_ConnectionFailed;
                //redis.ConnectionRestored += _Connection_ConnectionRestored;
                return redis;
            });

            var subscriber = _connection.Value.GetSubscriber();
            subscriber.Subscribe($"__keyspace@{DatabaseIndex}__:*", async (channel, value) =>
            {
                if (value == "expired")
                {
                    var tokens = channel.ToString().Split(HASH_SPLITTER);
                    if (tokens.Length >= 2)
                    {
                        string hashSet = tokens[0];
                        string hashKey = tokens[1];

                        int colon = hashSet.IndexOf(':');
                        if (colon >= 0)
                        {
                            hashSet = hashSet[(colon + 1)..];
                        }

                        // hash의 필드를 삭제해줌.
                        await Database.HashDeleteAsync(hashSet, hashKey);

                        long fieldCount = await Database.HashLengthAsync(hashSet);

                        Console.WriteLine($"EXPIRED: hashSet=\"{hashSet}\", hashKey=\"{hashKey}\", fieldCount={fieldCount}");
                    }
                }
            });
        }

        private void _Connection_ErrorMessage(object sender, RedisErrorEventArgs e)
        {
            throw new NotImplementedException();
        }

        // for the 'GetSubscriber()' and another Databases
        public IConnectionMultiplexer Connection => _connection.Value;

        // for the default database
        public IDatabase Database => Connection.GetDatabase(DatabaseIndex);

        public T Get<T>(RedisKey key, CommandFlags flags = CommandFlags.None)
        {
            RedisValue result = Database.StringGet(key, flags);
            if (!result.HasValue)
            {
                return default;
            }

            T value = JsonSerializer.Deserialize<T>(result);
            return value;
        }

        public async Task<T> GetAsync<T>(RedisKey key, CommandFlags flags = CommandFlags.None)
        {
            RedisValue result = await Database.StringGetAsync(key, flags);
            if (!result.HasValue)
            {
                return default;
            }

            T value = JsonSerializer.Deserialize<T>(result);
            return value;
        }

        public bool Set<T>(RedisKey key, object value, TimeSpan? expiry = null, When when = When.Always, CommandFlags flags = CommandFlags.None)
        {
            if (value == null)
            {
                return false;
            }

            return Database.StringSet(key, JsonSerializer.Serialize(value), expiry, when, flags);
        }

        public async Task<bool> SetAsync<T>(RedisKey key, object value, TimeSpan? expiry = null, When when = When.Always, CommandFlags flags = CommandFlags.None)
        {
            if (value == null)
            {
                return false;
            }

            return await Database.StringSetAsync(key, JsonSerializer.Serialize(value), expiry, when, flags);
        }

        public bool Delete(RedisKey key, CommandFlags flags = CommandFlags.None)
        {
            return Database.KeyDelete(key, flags);
        }

        public async Task<bool> DeleteAsync(RedisKey key, CommandFlags flags = CommandFlags.None)
        {
            return await Database.KeyDeleteAsync(key, flags);
        }



        private static string HashKeyString(string hash, RedisValue key)
        {
            return $"{hash}{HASH_SPLITTER}{key}";
        }

        public bool HashDelete(RedisKey key, CommandFlags flags = CommandFlags.None)
        {
            HashEntry[] entries = Database.HashGetAll(key, flags);
            if (entries != null && entries.Length > 0)
            {
                RedisKey[] keys = new RedisKey[entries.Length];
                for (int i = 0; i < entries.Length; i++)
                {
                    keys[i] = HashKeyString(key, entries[i].Name);
                }

                Database.KeyDelete(keys, flags);
            }

            return Database.KeyDelete(key, flags);
        }


		//@todo hash field별 ttl을 체크할 수 있어야함.

		private class HashField
		{
			public DateTime ExpireTime;
			public object Value;
		}

        public async Task<bool> HashDeleteAsync(RedisKey key, CommandFlags flags = CommandFlags.None)
        {
            HashEntry[] entries = await Database.HashGetAllAsync(key, flags);
            if (entries != null && entries.Length > 0)
            {
                RedisKey[] keys = new RedisKey[entries.Length];
                for (int i = 0; i < entries.Length; i++)
                {
                    Console.WriteLine(entries[i].Name);
                    keys[i] = HashKeyString(key, entries[i].Name);
                    //여러개의 TTL을 구할수 있지 않을까?
                }

                //TTL 조회는 키당 즉, 하나씩 밖에 안되는건가?

                await Database.KeyDeleteAsync(keys, flags);
            }

            return await Database.KeyDeleteAsync(key, flags);
        }

        public bool HashSetFieldExpire(string hash, RedisValue key, TimeSpan expiry, CommandFlags flags = CommandFlags.None)
        {
			//deserialize한후 HashField의 ExpireTime을 수정해주어야함.
			//뭔가 비효율적이네..
			//종료후에 모든 키를 날려버린다고 가정해버리면 문제될건 없는데..

            string hashKey = HashKeyString(hash, key);
            return Database.KeyExpire(hashKey, expiry, flags);
        }

        public async Task<bool> HashSetFieldExpireAsync(string hash, RedisValue key, TimeSpan expiry, CommandFlags flags = CommandFlags.None)
        {
			//deserialize한후 HashField의 ExpireTime을 수정해주어야함.
			//뭔가 비효율적이네..

            string hashKey = HashKeyString(hash, key);
            return await Database.KeyExpireAsync(hashKey, expiry, flags);
        }


        //todo 이중으로 serialize해서 타임아웃을 처리할까?

        public T HashGetField<T>(string hash, RedisValue key, CommandFlags flags = CommandFlags.None)
        {
            var result = Database.HashGet(hash, key.ToString(), flags);
            return JsonSerializer.Deserialize<T>(result);
        }

        public async Task<T> HashGetFieldAsync<T>(string hash, RedisValue key, CommandFlags flags = CommandFlags.None)
        {
            var result = await Database.HashGetAsync(hash, key.ToString(), flags);
            return JsonSerializer.Deserialize<T>(result);
        }

        public bool HashSetField<T>(string hash, RedisValue key, object value, TimeSpan? expiry = null, When when = When.Always, CommandFlags flags = CommandFlags.None)
        {
            //todo expiry가 null이면 기본 타임아웃을 설정하는 형태로 처리하는게 좋을듯.
            if (expiry == null)
            {
                expiry = DefaultExpiry;
            }

            //@todo 기존 새도우 키의 TTL이 키의 TTL보다 작으면 재설정해줘야함.
            //TTL을 구한다음
            //TTL보다 적으면 갱신.

            string hashKey = HashKeyString(hash, key);
            Database.StringSet(hashKey, RedisValue.EmptyString, when: when, flags: flags, expiry: expiry); // 값은 필요없음. (단, null로 넣으면 엔트리 자체가 안생기므로 빈문자열로)

            Database.HashSet(hash, key.ToString(), JsonSerializer.Serialize(value), when, flags);

            return true;
        }

        public async Task<bool> HashSetFieldAsync<T>(string hash, RedisValue key, object value, TimeSpan? expiry = null, When when = When.Always, CommandFlags flags = CommandFlags.None)
        {
            //todo expiry가 null이면 기본 타임아웃을 설정하는 형태로 처리하는게 좋을듯.
            if (expiry == null)
            {
                expiry = DefaultExpiry;
            }

            string hashKey = HashKeyString(hash, key);
            await Database.StringSetAsync(hashKey, RedisValue.EmptyString, when: when, flags: flags, expiry: expiry); // 값은 필요없음. (단, null로 넣으면 엔트리 자체가 안생기므로 빈문자열로)

            await Database.HashSetAsync(hash, key.ToString(), JsonSerializer.Serialize(value), when, flags);

            return true;
        }


        public bool HashDeleteField(string hash, RedisValue key, CommandFlags flags = CommandFlags.None)
        {
            return Database.HashDelete(hash, key, flags);
        }

        public async Task<bool> HashDeleteFieldAsync(string hash, RedisValue key, CommandFlags flags = CommandFlags.None)
        {
            var result = await Database.HashDeleteAsync(hash, key, flags);
            return result;
        }


        public RedisValue[] HashGetKeys(string hash, CommandFlags flags = CommandFlags.None)
        {
            return Database.HashKeys(hash);
        }

        public async Task<RedisValue[]> HashGetKeysAsync(string hash, CommandFlags flags = CommandFlags.None)
        {
            return await Database.HashKeysAsync(hash);
        }


        public RedisValue[] HashGetValues(string hash, CommandFlags flags = CommandFlags.None)
        {
            return Database.HashValues(hash);
        }

        public async Task<RedisValue[]> HashGetValuesAsync(string hash, CommandFlags flags = CommandFlags.None)
        {
            return await Database.HashValuesAsync(hash);
        }

        public HashEntry[] HashGetAll(string hash, CommandFlags flags = CommandFlags.None)
        {
            return Database.HashGetAll(hash, flags);
        }

        public async Task<HashEntry[]> HashGetAllAsync(string hash, CommandFlags flags = CommandFlags.None)
        {
            return await Database.HashGetAllAsync(hash, flags);
        }


        public TimeSpan Ping()
        {
            return Database.Ping();
        }

        public async Task<TimeSpan> PingAsync()
        {
            return await Database.PingAsync();
        }
    }
}
