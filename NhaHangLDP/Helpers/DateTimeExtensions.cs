using System;

namespace NhaHangLDP.Helpers
{
    /// <summary>
    /// Extension methods cho chuyển đổi types DateTime/DateOnly và TimeSpan/TimeOnly
    /// </summary>
    public static class DateTimeExtensions
    {
        // DateOnly ↔ DateTime conversions
        public static DateOnly ToDateOnly(this DateTime dateTime)
        {
            return DateOnly.FromDateTime(dateTime);
        }

        public static DateOnly? ToDateOnly(this DateTime? dateTime)
        {
            return dateTime.HasValue ? DateOnly.FromDateTime(dateTime.Value) : null;
        }

        public static DateTime ToDateTime(this DateOnly dateOnly)
        {
            return dateOnly.ToDateTime(TimeOnly.MinValue);
        }

        public static DateTime? ToDateTime(this DateOnly? dateOnly)
        {
            return dateOnly?.ToDateTime(TimeOnly.MinValue);
        }

        // TimeOnly ↔ TimeSpan conversions
        public static TimeOnly ToTimeOnly(this TimeSpan timeSpan)
        {
            return TimeOnly.FromTimeSpan(timeSpan);
        }

        public static TimeOnly? ToTimeOnly(this TimeSpan? timeSpan)
        {
            return timeSpan.HasValue ? TimeOnly.FromTimeSpan(timeSpan.Value) : null;
        }

        public static TimeSpan ToTimeSpan(this TimeOnly timeOnly)
        {
            return timeOnly.ToTimeSpan();
        }

        public static TimeSpan? ToTimeSpan(this TimeOnly? timeOnly)
        {
            return timeOnly?.ToTimeSpan();
        }

        // TotalMinutes for TimeOnly
        public static double TotalMinutes(this TimeOnly timeOnly)
        {
            return timeOnly.ToTimeSpan().TotalMinutes;
        }

        public static double TotalHours(this TimeOnly timeOnly)
        {
            return timeOnly.ToTimeSpan().TotalHours;
        }

        // Date property for DateOnly (returns itself - no-op for compatibility)
        public static DateOnly Date(this DateOnly dateOnly)
        {
            return dateOnly;
        }

        // Comparison helpers
        public static bool IsGreaterThanOrEqual(this DateOnly dateOnly, DateTime dateTime)
        {
            return dateOnly >= DateOnly.FromDateTime(dateTime);
        }

        public static bool IsLessThanOrEqual(this DateOnly dateOnly, DateTime dateTime)
        {
            return dateOnly <= DateOnly.FromDateTime(dateTime);
        }

        public static bool IsGreaterThan(this DateOnly dateOnly, DateTime dateTime)
        {
            return dateOnly > DateOnly.FromDateTime(dateTime);
        }

        public static bool IsLessThan(this DateOnly dateOnly, DateTime dateTime)
        {
            return dateOnly < DateOnly.FromDateTime(dateTime);
        }

        public static int DaysDifference(this DateOnly endDate, DateOnly startDate)
        {
            return endDate.DayNumber - startDate.DayNumber;
        }

        // Hour and Minute for TimeOnly (already exist but adding for clarity)
        public static int Hours(this TimeOnly timeOnly)
        {
            return timeOnly.Hour;
        }

        public static int Minutes(this TimeOnly timeOnly)
        {
            return timeOnly.Minute;
        }
    }
}
