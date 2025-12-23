using System.Collections.Generic;

namespace NhaHangLDP.Helpers
{
    public static class StatusTextHelper
    {
        private static readonly Dictionary<string, string> StatusMap = new Dictionary<string, string>
        {
            { "Pending", "Chờ xử lý" },
            { "Preparing", "Đang chuẩn bị" },
            { "Ready", "Sẵn sàng" },
            { "Completed", "Hoàn thành" },
            { "Cancelled", "Đã hủy" }
        };

        public static string GetVietnameseStatus(string status)
        {
            return StatusMap.ContainsKey(status) ? StatusMap[status] : status;
        }

        public static string NormalizeStatus(string status)
        {
            if (string.IsNullOrEmpty(status))
                return status;

            return char.ToUpper(status[0]) + status.Substring(1).ToLower();
        }
    }
}
