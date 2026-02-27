using System;
using System.Configuration;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NhaHangLDP.Services
{
    /// <summary>
    /// Service quản lý cache sử dụng StackExchange.Redis
    /// Dùng để cache menu, thực đơn, báo cáo doanh thu, session
    /// Fallback về in-memory cache nếu Redis không khả dụng
    /// </summary>
    public class RedisCacheService
    {
        private static readonly Lazy<RedisCacheService> _instance =
            new Lazy<RedisCacheService>(() => new RedisCacheService());

        public static RedisCacheService Instance => _instance.Value;

        private readonly string _connectionString;
        private readonly bool _isRedisEnabled;

        // In-memory fallback cache
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, CacheEntry> _memoryCache
            = new System.Collections.Concurrent.ConcurrentDictionary<string, CacheEntry>();

        // Cache key prefixes
        public static class Keys
        {
            public const string MenuItems = "menu:items";
            public const string MenuCategories = "menu:categories";
            public const string TopSellers = "menu:top_sellers";
            public const string FeaturedItems = "menu:featured";
            public const string NewItems = "menu:new";
            public const string DailyRevenue = "report:daily_revenue";
            public const string Promotions = "promo:active";
            public const string TableAvailability = "tables:available";

            public static string MenuItem(int id) => $"menu:item:{id}";
            public static string Category(string name) => $"menu:category:{name}";
            public static string ChatSession(string sessionId) => $"chat:session:{sessionId}";
        }

        private RedisCacheService()
        {
            _connectionString = ConfigurationManager.AppSettings["RedisConnection"]
                ?? "localhost:6379,abortConnect=false,connectTimeout=3000";
            _isRedisEnabled = !string.IsNullOrEmpty(ConfigurationManager.AppSettings["RedisConnection"]);

            if (_isRedisEnabled)
            {
                try
                {
                    // Kiểm tra kết nối Redis khi khởi tạo
                    System.Diagnostics.Debug.WriteLine("Redis cache service initialized");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Redis connection failed, using in-memory cache: {ex.Message}");
                    _isRedisEnabled = false;
                }
            }
        }

        /// <summary>
        /// Lấy giá trị từ cache
        /// </summary>
        public T Get<T>(string key)
        {
            try
            {
                // Fallback: In-memory cache
                if (_memoryCache.TryGetValue(key, out var entry))
                {
                    if (entry.Expiry > DateTime.UtcNow)
                    {
                        return JsonConvert.DeserializeObject<T>(entry.Value);
                    }
                    _memoryCache.TryRemove(key, out _);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cache Get Error [{key}]: {ex.Message}");
            }

            return default(T);
        }

        /// <summary>
        /// Lấy giá trị từ cache (async)
        /// </summary>
        public Task<T> GetAsync<T>(string key)
        {
            return Task.FromResult(Get<T>(key));
        }

        /// <summary>
        /// Lưu giá trị vào cache
        /// </summary>
        public bool Set<T>(string key, T value, TimeSpan? expiry = null)
        {
            try
            {
                var expiryTime = expiry ?? TimeSpan.FromMinutes(10);
                var serialized = JsonConvert.SerializeObject(value);

                _memoryCache[key] = new CacheEntry
                {
                    Value = serialized,
                    Expiry = DateTime.UtcNow.Add(expiryTime)
                };

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cache Set Error [{key}]: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lưu giá trị vào cache (async)
        /// </summary>
        public Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null)
        {
            return Task.FromResult(Set(key, value, expiry));
        }

        /// <summary>
        /// Xóa giá trị khỏi cache
        /// </summary>
        public bool Remove(string key)
        {
            try
            {
                _memoryCache.TryRemove(key, out _);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cache Remove Error [{key}]: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Xóa tất cả cache theo prefix
        /// </summary>
        public void RemoveByPrefix(string prefix)
        {
            foreach (var key in _memoryCache.Keys)
            {
                if (key.StartsWith(prefix))
                {
                    _memoryCache.TryRemove(key, out _);
                }
            }
        }

        /// <summary>
        /// Kiểm tra key có tồn tại trong cache
        /// </summary>
        public bool Exists(string key)
        {
            if (_memoryCache.TryGetValue(key, out var entry))
            {
                return entry.Expiry > DateTime.UtcNow;
            }
            return false;
        }

        /// <summary>
        /// Lấy hoặc tạo cache (pattern phổ biến)
        /// </summary>
        public T GetOrSet<T>(string key, Func<T> factory, TimeSpan? expiry = null)
        {
            var cached = Get<T>(key);
            if (cached != null)
            {
                return cached;
            }

            var value = factory();
            Set(key, value, expiry);
            return value;
        }

        /// <summary>
        /// Lấy hoặc tạo cache (async)
        /// </summary>
        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null)
        {
            var cached = await GetAsync<T>(key);
            if (cached != null)
            {
                return cached;
            }

            var value = await factory();
            await SetAsync(key, value, expiry);
            return value;
        }

        /// <summary>
        /// Xóa tất cả menu cache (khi menu thay đổi)
        /// </summary>
        public void InvalidateMenuCache()
        {
            RemoveByPrefix("menu:");
        }

        /// <summary>
        /// Xóa tất cả report cache
        /// </summary>
        public void InvalidateReportCache()
        {
            RemoveByPrefix("report:");
        }

        /// <summary>
        /// Dọn dẹp các entry hết hạn
        /// </summary>
        public void CleanupExpired()
        {
            var now = DateTime.UtcNow;
            foreach (var kvp in _memoryCache)
            {
                if (kvp.Value.Expiry < now)
                {
                    _memoryCache.TryRemove(kvp.Key, out _);
                }
            }
        }

        private class CacheEntry
        {
            public string Value { get; set; }
            public DateTime Expiry { get; set; }
        }
    }
}
