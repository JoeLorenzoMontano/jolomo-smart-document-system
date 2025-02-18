using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/redis")]
public class RedisCacheController : ControllerBase {
  private readonly RedisCacheService _redisCacheService;

  public RedisCacheController(RedisCacheService redisCacheService) {
    _redisCacheService = redisCacheService;
  }

  /// <summary>
  /// Check if a key exists in Redis and return whether it's a cache hit or miss.
  /// </summary>
  [HttpGet("check/{key}")]
  public async Task<IActionResult> CheckCache(string key) {
    var value = await _redisCacheService.GetAsync(key);
    if(string.IsNullOrEmpty(value)) {
      return NotFound(new { message = "Cache miss", key });
    }
    return Ok(new { message = "Cache hit", key, value });
  }

  /// <summary>
  /// Store a key-value pair in Redis manually for testing.
  /// </summary>
  [HttpPost("store")]
  public async Task<IActionResult> StoreCache([FromQuery] string key, [FromQuery] string value) {
    await _redisCacheService.SetAsync(key, value);
    return Ok(new { message = "Stored in cache", key, value });
  }

  /// <summary>
  /// Retrieve a cached value from Redis.
  /// </summary>
  [HttpGet("get/{key}")]
  public async Task<IActionResult> GetCache(string key) {
    var value = await _redisCacheService.GetAsync(key);
    if(string.IsNullOrEmpty(value)) {
      return NotFound(new { message = "Key not found in Redis", key });
    }
    return Ok(new { key, value });
  }

  /// <summary>
  /// Remove a specific key from Redis cache.
  /// </summary>
  [HttpDelete("delete/{key}")]
  public async Task<IActionResult> DeleteCache(string key) {
    var result = await _redisCacheService.DeleteAsync(key);
    if(result) {
      return Ok(new { message = "Deleted from cache", key });
    }
    return NotFound(new { message = "Key not found", key });
  }

  /// <summary>
  /// Empties the entire Redis cache.
  /// </summary>
  [HttpDelete("clear")]
  public async Task<IActionResult> ClearAllCache() {
    var result = await _redisCacheService.ClearAllCache();
    if(result) {
      return Ok(new { message = "Redis cache has been cleared." });
    }
    return StatusCode(500, new { message = "Failed to clear Redis cache." });
  }
}
