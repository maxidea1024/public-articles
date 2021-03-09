using StackExchange.Redis;
using System;
using System.Threading.Tasks;

namespace P2TestApp.Cache
{
    public interface ICacheClient
    {
        T Get<T>(RedisKey key, CommandFlags flags = CommandFlags.None);
        Task<T> GetAsync<T>(RedisKey key, CommandFlags flags = CommandFlags.None);


        bool Set<T>(RedisKey key, object value, TimeSpan? expiry = null, When when = When.Always, CommandFlags flags = CommandFlags.None);
        Task<bool> SetAsync<T>(RedisKey key, object value, TimeSpan? expiry = null, When when = When.Always, CommandFlags flags = CommandFlags.None);


        bool Delete(RedisKey key, CommandFlags flags = CommandFlags.None);
        Task<bool> DeleteAsync(RedisKey key, CommandFlags flags = CommandFlags.None);


        bool HashDelete(RedisKey key, CommandFlags flags = CommandFlags.None);
        Task<bool> HashDeleteAsync(RedisKey key, CommandFlags flags = CommandFlags.None);


        bool HashSetFieldExpire(string hash, RedisValue key, TimeSpan expiry, CommandFlags flags = CommandFlags.None);
        Task<bool> HashSetFieldExpireAsync(string hash, RedisValue key, TimeSpan expiry, CommandFlags flags = CommandFlags.None);


        T HashGetField<T>(string hash, RedisValue key, CommandFlags flags = CommandFlags.None);
        Task<T> HashGetFieldAsync<T>(string hash, RedisValue key, CommandFlags flags = CommandFlags.None);


        bool HashSetField<T>(string hash, RedisValue key, object value, TimeSpan? expiry = null, When when = When.Always, CommandFlags flags = CommandFlags.None);
        Task<bool> HashSetFieldAsync<T>(string hash, RedisValue key, object value, TimeSpan? expiry = null, When when = When.Always, CommandFlags flags = CommandFlags.None);


        bool HashDeleteField(string hash, RedisValue key, CommandFlags flags = CommandFlags.None);
        Task<bool> HashDeleteFieldAsync(string hash, RedisValue key, CommandFlags flags = CommandFlags.None);


        RedisValue[] HashGetKeys(string hash, CommandFlags flags = CommandFlags.None);
        Task<RedisValue[]> HashGetKeysAsync(string hash, CommandFlags flags = CommandFlags.None);

        RedisValue[] HashGetValues(string hash, CommandFlags flags = CommandFlags.None);
        Task<RedisValue[]> HashGetValuesAsync(string hash, CommandFlags flags = CommandFlags.None);


        HashEntry[] HashGetAll(string hash, CommandFlags flags = CommandFlags.None);
        Task<HashEntry[]> HashGetAllAsync(string hash, CommandFlags flags = CommandFlags.None);


        //서버 재기동시에 타임아웃된 엔트리들을 수동으로 지워주는 기능을 넣어주자.

        TimeSpan Ping();
        Task<TimeSpan> PingAsync();
    }
}
