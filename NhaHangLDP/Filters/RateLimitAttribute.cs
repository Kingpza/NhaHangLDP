using System;
using System.Collections.Concurrent;
using System.Net;
using System.Web;
using System.Web.Mvc;

namespace NhaHangLDP.Filters
{
    /// <summary>
    /// Rate Limiting filter cho ASP.NET MVC
    /// Giới hạn số lượng request trong một khoảng thời gian
    /// Bảo vệ khỏi spam đặt bàn, DDoS, và lạm dụng API
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
    public class RateLimitAttribute : ActionFilterAttribute
    {
        /// <summary>
        /// Số request tối đa trong khoảng thời gian
        /// </summary>
        public int MaxRequests { get; set; }

        /// <summary>
        /// Khoảng thời gian (giây)
        /// </summary>
        public int TimeWindowSeconds { get; set; }

        /// <summary>
        /// Thông báo khi bị giới hạn
        /// </summary>
        public string Message { get; set; }

        // Thread-safe dictionary lưu request count
        private static readonly ConcurrentDictionary<string, RateLimitEntry> _requestStore
            = new ConcurrentDictionary<string, RateLimitEntry>();

        public RateLimitAttribute()
        {
            MaxRequests = 10;
            TimeWindowSeconds = 60;
            Message = "Bạn đã gửi quá nhiều yêu cầu. Vui lòng thử lại sau.";
        }

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var clientKey = GetClientKey(filterContext.HttpContext);
            var actionKey = filterContext.ActionDescriptor.ActionName;
            var key = $"{clientKey}:{actionKey}";

            var now = DateTime.UtcNow;
            var entry = _requestStore.GetOrAdd(key, _ => new RateLimitEntry(now));

            lock (entry)
            {
                // Reset nếu đã hết thời gian
                if ((now - entry.WindowStart).TotalSeconds > TimeWindowSeconds)
                {
                    entry.WindowStart = now;
                    entry.RequestCount = 0;
                }

                entry.RequestCount++;

                if (entry.RequestCount > MaxRequests)
                {
                    var retryAfter = TimeWindowSeconds - (int)(now - entry.WindowStart).TotalSeconds;

                    if (filterContext.HttpContext.Request.IsAjaxRequest())
                    {
                        filterContext.Result = new JsonResult
                        {
                            Data = new
                            {
                                success = false,
                                response = Message,
                                retryAfter = retryAfter
                            },
                            JsonRequestBehavior = JsonRequestBehavior.AllowGet
                        };
                    }
                    else
                    {
                        filterContext.Result = new HttpStatusCodeResult(
                            (int)HttpStatusCode.TooManyRequests,
                            Message);
                    }
                    return;
                }
            }

            base.OnActionExecuting(filterContext);
        }

        private string GetClientKey(HttpContextBase context)
        {
            // Ưu tiên X-Forwarded-For cho proxy/load balancer
            var ip = context.Request.Headers["X-Forwarded-For"];
            if (string.IsNullOrEmpty(ip))
            {
                ip = context.Request.UserHostAddress;
            }
            else
            {
                // Lấy IP đầu tiên nếu có nhiều
                ip = ip.Split(',')[0].Trim();
            }
            return ip ?? "unknown";
        }

        /// <summary>
        /// Dọn dẹp các entry cũ (gọi định kỳ)
        /// </summary>
        public static void CleanupExpiredEntries(int maxAgeSeconds = 300)
        {
            var cutoff = DateTime.UtcNow.AddSeconds(-maxAgeSeconds);
            foreach (var kvp in _requestStore)
            {
                if (kvp.Value.WindowStart < cutoff)
                {
                    _requestStore.TryRemove(kvp.Key, out _);
                }
            }
        }

        private class RateLimitEntry
        {
            public DateTime WindowStart { get; set; }
            public int RequestCount { get; set; }

            public RateLimitEntry(DateTime start)
            {
                WindowStart = start;
                RequestCount = 0;
            }
        }
    }
}
