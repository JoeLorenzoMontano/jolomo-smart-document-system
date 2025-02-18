using StackExchange.Redis;
using Microsoft.Extensions.Configuration;

public class RedisCacheService {
  private readonly ConnectionMultiplexer _redis;
  private readonly IDatabase _db;
  private readonly int _cacheExpirationMinutes;

  public RedisCacheService(IConfiguration configuration) {
    string redisHost = configuration["Redis:Host"] ?? "localhost";
    string redisPort = configuration["Redis:Port"] ?? "6379";
    _cacheExpirationMinutes = int.Parse(configuration["Redis:CacheExpirationMinutes"] ?? "60");
    _redis = ConnectionMultiplexer.Connect($"{redisHost}:{redisPort}");
    _db = _redis.GetDatabase();
  }

  public async Task SetAsync(string key, string value) {
    await _db.StringSetAsync(key, value, TimeSpan.FromMinutes(_cacheExpirationMinutes));
  }

  public async Task<string?> GetAsync(string key) {
    return await _db.StringGetAsync(key);
  }

  public async Task<bool> DeleteAsync(string key) {
    return await _db.KeyDeleteAsync(key);
  }
  public async Task<bool> ClearAllCache() {
    try {
      await _db.ExecuteAsync("FLUSHDB");
      Console.WriteLine("[RedisCacheService] Entire cache has been cleared.");
      return true;
    }
    catch(Exception ex) {
      Console.WriteLine($"[RedisCacheService] Error clearing cache: {ex.Message}");
      return false;
    }
  }
}
