using System;

namespace NhaHangLDP.Services.Reports
{
    public static class DateRangeHelper
    {
        public static void GetDateRange(string period, out DateTime startDate, out DateTime endDate)
        {
            var today = DateTime.Today;

            switch (period?.ToLower())
            {
                case "week":
                    startDate = today.AddDays(-(int)today.DayOfWeek);
                    endDate = startDate.AddDays(7);
                    break;
                case "month":
                    startDate = new DateTime(today.Year, today.Month, 1);
                    endDate = startDate.AddMonths(1);
                    break;
                case "quarter":
                    var quarter = (today.Month - 1) / 3 + 1;
                    startDate = new DateTime(today.Year, (quarter - 1) * 3 + 1, 1);
                    endDate = startDate.AddMonths(3);
                    break;
                case "year":
                    startDate = new DateTime(today.Year, 1, 1);
                    endDate = new DateTime(today.Year + 1, 1, 1);
                    break;
                default:
                    startDate = today;
                    endDate = today.AddDays(1);
                    break;
            }
        }

        public static void GetPreviousDateRange(DateTime currentStart, string period, out DateTime prevStart, out DateTime prevEnd)
        {
            switch (period?.ToLower())
            {
                case "week":
                    prevStart = currentStart.AddDays(-7);
                    prevEnd = currentStart;
                    break;
                case "month":
                    prevStart = currentStart.AddMonths(-1);
                    prevEnd = currentStart;
                    break;
                case "quarter":
                    prevStart = currentStart.AddMonths(-3);
                    prevEnd = currentStart;
                    break;
                case "year":
                    prevStart = currentStart.AddYears(-1);
                    prevEnd = currentStart;
                    break;
                default:
                    prevStart = currentStart.AddDays(-1);
                    prevEnd = currentStart;
                    break;
            }
        }

        public static string GetPeriodDisplayName(string period)
        {
            switch (period?.ToLower())
            {
                case "today": return "Hôm nay";
                case "week": return "Tuần này";
                case "month": return "Tháng này";
                case "quarter": return "Quý này";
                case "year": return "Năm này";
                case "prev_month": return "Tháng trước";
                case "prev_week": return "Tuần trước";
                default: return period;
            }
        }

        public static string FormatCurrency(decimal amount)
        {
            return amount.ToString("N0", new System.Globalization.CultureInfo("vi-VN")) + " VNĐ";
        }
    }
}
