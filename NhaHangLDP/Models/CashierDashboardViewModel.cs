using NhaHangLDP.Data.Entities;
using System;
using System.ComponentModel.DataAnnotations;

namespace NhaHangLDP.Models
{
    public class CashierDashboardViewModel
    {
        /// <summary>
        /// Số lượng đơn hàng đang hoạt động (Pending, Preparing, Ready)
        /// </summary>
        public int ActiveOrderCount { get; set; }

        /// <summary>
        /// Tổng số đơn hàng trong ca hiện tại
        /// </summary>
        public int TotalOrdersToday { get; set; }

        /// <summary>
        /// Doanh thu ca hiện tại
        /// </summary>
        public decimal TodayRevenue { get; set; }

        /// <summary>
        /// Số bàn đang được sử dụng (Status = "Occupied")
        /// </summary>
        public int OccupiedTables { get; set; }

        /// <summary>
        /// Tổng số bàn
        /// </summary>
        public int TotalTables { get; set; }

        /// <summary>
        /// Số bàn đã được đặt trước (Status = "Reserved")
        /// </summary>
        public int ReservedTables { get; set; }

        /// <summary>
        /// Số bàn trống (Status = "Available")
        /// </summary>
        public int AvailableTables { get; set; }

        /// <summary>
        /// Số đơn hàng chờ xử lý (Status = "Pending")
        /// </summary>
        public int PendingOrderCount { get; set; }

        /// <summary>
        /// Thông tin ca làm việc hiện tại
        /// </summary>
        public CashierShift ActiveShift { get; set; }

        /// <summary>
        /// Thời gian bắt đầu ca làm việc
        /// </summary>
        public DateTime? ShiftStartTime { get; set; }

        /// <summary>
        /// Trạng thái ca làm việc có hoạt động không
        /// </summary>
        public bool HasActiveShift { get; set; }

        /// <summary>
        /// Tên thu ngân hiện tại
        /// </summary>
        public string CashierName { get; set; }

        /// <summary>
        /// Thông tin bàn đang sử dụng / tổng số bàn (format: "3/12")
        /// </summary>
        public string TableUsageDisplay => $"{OccupiedTables}/{TotalTables}";

        /// <summary>
        /// Thông tin chi tiết về bàn (format: "Trống: 5 | Có khách: 3 | Đã đặt: 2")
        /// </summary>
        public string TableStatusSummary => $"Trống: {AvailableTables} | Có khách: {OccupiedTables} | Đã đặt: {ReservedTables}";

        /// <summary>
        /// Doanh thu hiển thị với định dạng VNĐ
        /// </summary>
        public string TodayRevenueDisplay => TodayRevenue.ToString("#,##0") + " VNĐ";

        /// <summary>
        /// Constructor mặc định
        /// </summary>
        public CashierDashboardViewModel()
        {
            ActiveOrderCount = 0;
            TotalOrdersToday = 0;
            TodayRevenue = 0;
            OccupiedTables = 0;
            TotalTables = 0;
            ReservedTables = 0;
            AvailableTables = 0;
            PendingOrderCount = 0;
            HasActiveShift = false;
            CashierName = "Thu Ngân";
        }
    }
}