using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NhaHangLDP.Models
{
    // ============================================
    // KHÔNG CẦN ĐỊNH NGHĨA LẠI ENTITY CLASSES
    // Các entity đã được tự động tạo bởi EDMX:
    // - Attendance
    // - LeaveRequest  
    // - EmployeeContract
    // - Payroll
    // - PerformanceReview
    // - WorkShift
    // - EmployeeSchedule
    // ============================================

    #region View Models

    /// <summary>
    /// ViewModel cho Dashboard HR
    /// </summary>
    public class HRDashboardViewModel
    {
        public int TotalEmployees { get; set; }
        public int ActiveEmployees { get; set; }
        public int InactiveEmployees { get; set; }
        public int NewEmployeesThisMonth { get; set; }
        public decimal AttendanceRate { get; set; }
        public int PendingLeaveRequests { get; set; }
        public int TodayPresent { get; set; }
        public int TodayAbsent { get; set; }
        public int TodayLate { get; set; }
        public decimal AverageWorkHours { get; set; }
        public decimal TotalPayrollThisMonth { get; set; }

        public List<AttendanceSummaryItem> TodayAttendance { get; set; }
        public List<EmployeeByRoleItem> EmployeesByRole { get; set; }
        public List<LeaveRequestItem> RecentLeaveRequests { get; set; }
        public List<TopPerformerItem> TopPerformers { get; set; }
        public List<UpcomingBirthdayItem> UpcomingBirthdays { get; set; }
        public List<AttendanceTrendItem> AttendanceTrend { get; set; }
    }

    public class AttendanceSummaryItem
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public string RoleName { get; set; }
        public DateTime? CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }
        public decimal? WorkHours { get; set; }
        public string Status { get; set; }
        public string StatusClass { get; set; }
    }

    public class EmployeeByRoleItem
    {
        public string RoleName { get; set; }
        public int Count { get; set; }
        public string Color { get; set; }
    }

    public class LeaveRequestItem
    {
        public int Id { get; set; }
        public string EmployeeName { get; set; }
        public string LeaveType { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int Days { get; set; }
        public string Status { get; set; }
        public string StatusClass { get; set; }
        public string Reason { get; set; }
    }

    public class TopPerformerItem
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public string RoleName { get; set; }
        public decimal OverallScore { get; set; }
        public int ReviewCount { get; set; }
    }

    public class UpcomingBirthdayItem
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public DateTime BirthDate { get; set; }
        public int DaysUntil { get; set; }
    }

    public class AttendanceTrendItem
    {
        public string Label { get; set; }
        public int Present { get; set; }
        public int Late { get; set; }
        public int Absent { get; set; }
        public decimal AttendanceRate { get; set; }
    }

    /// <summary>
    /// ViewModel cho form Check-in/Check-out
    /// </summary>
    public class AttendanceFormViewModel
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public string Action { get; set; }
        public DateTime CurrentTime { get; set; }
        public string Note { get; set; }
        public Attendance TodayAttendance { get; set; }
    }

    /// <summary>
    /// ViewModel cho báo cáo chấm công
    /// </summary>
    public class AttendanceReportViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int? EmployeeId { get; set; }
        public List<AttendanceReportItem> AttendanceRecords { get; set; }
        public AttendanceSummaryStats Summary { get; set; }
        public List<Employee> Employees { get; set; }
    }

    public class AttendanceReportItem
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public string RoleName { get; set; }
        public DateTime Date { get; set; }
        public DateTime? CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }
        public decimal? WorkHours { get; set; }
        public string Status { get; set; }
        public string Note { get; set; }
    }

    public class AttendanceSummaryStats
    {
        public int TotalDays { get; set; }
        public int PresentDays { get; set; }
        public int LateDays { get; set; }
        public int AbsentDays { get; set; }
        public decimal TotalWorkHours { get; set; }
        public decimal AverageWorkHours { get; set; }
        public decimal AttendanceRate { get; set; }
    }

    /// <summary>
    /// ViewModel cho quản lý đơn xin nghỉ
    /// </summary>
    public class LeaveManagementViewModel
    {
        public List<LeaveRequestItem> PendingRequests { get; set; }
        public List<LeaveRequestItem> ApprovedRequests { get; set; }
        public List<LeaveRequestItem> RejectedRequests { get; set; }
        public LeaveBalanceStats LeaveStats { get; set; }
    }

    public class LeaveBalanceStats
    {
        public int TotalAnnualLeave { get; set; }
        public int UsedAnnualLeave { get; set; }
        public int RemainingAnnualLeave { get; set; }
        public int SickLeaveUsed { get; set; }
        public int UnpaidLeaveUsed { get; set; }
    }

    /// <summary>
    /// ViewModel cho form xin nghỉ phép
    /// </summary>
    public class LeaveRequestFormViewModel
    {
        public LeaveRequest LeaveRequest { get; set; }
        public LeaveBalanceStats CurrentBalance { get; set; }
        public List<LeaveTypeOption> LeaveTypes { get; set; }
    }

    public class LeaveTypeOption
    {
        public string Value { get; set; }
        public string Text { get; set; }
        public string Description { get; set; }
    }

    /// <summary>
    /// ViewModel cho bảng lương
    /// </summary>
    public class PayrollViewModel
    {
        public int Month { get; set; }
        public int Year { get; set; }
        public List<PayrollItem> PayrollItems { get; set; }
        public PayrollSummary Summary { get; set; }
    }

    public class PayrollItem
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public string RoleName { get; set; }
        public decimal BaseSalary { get; set; }
        public decimal Allowance { get; set; }
        public decimal OvertimeBonus { get; set; }
        public decimal PerformanceBonus { get; set; }
        public decimal TotalBonus { get; set; }
        public decimal Deduction { get; set; }
        public decimal NetSalary { get; set; }
        public decimal TotalWorkDays { get; set; }
        public decimal TotalWorkHours { get; set; }
        public int LateCount { get; set; }
        public int AbsentCount { get; set; }
        public string Status { get; set; }
        public string StatusClass { get; set; }
    }

    public class PayrollSummary
    {
        public int TotalEmployees { get; set; }
        public decimal TotalBaseSalary { get; set; }
        public decimal TotalBonus { get; set; }
        public decimal TotalDeduction { get; set; }
        public decimal TotalNetSalary { get; set; }
        public int PaidCount { get; set; }
        public int PendingCount { get; set; }
    }

    /// <summary>
    /// ViewModel cho đánh giá hiệu suất
    /// </summary>
    public class PerformanceReviewViewModel
    {
        public List<PerformanceReviewItem> Reviews { get; set; }
        public PerformanceSummary Summary { get; set; }
    }

    public class PerformanceReviewItem
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public string RoleName { get; set; }
        public string ReviewerName { get; set; }
        public DateTime ReviewDate { get; set; }
        public string ReviewPeriod { get; set; }
        public int ServiceQuality { get; set; }
        public int Punctuality { get; set; }
        public int Teamwork { get; set; }
        public int Communication { get; set; }
        public int WorkEfficiency { get; set; }
        public decimal OverallScore { get; set; }
        public string PerformanceLevel { get; set; }
        public string Comments { get; set; }
    }

    public class PerformanceSummary
    {
        public decimal AverageScore { get; set; }
        public int ExcellentCount { get; set; }
        public int GoodCount { get; set; }
        public int AverageCount { get; set; }
        public int BelowAverageCount { get; set; }
        public int PoorCount { get; set; }
    }

    /// <summary>
    /// ViewModel cho form đánh giá
    /// </summary>
    public class PerformanceReviewFormViewModel
    {
        public PerformanceReview Review { get; set; }
        public List<Employee> Employees { get; set; }
        public bool IsEdit { get; set; }
    }

    /// <summary>
    /// ViewModel cho lịch làm việc
    /// </summary>
    public class ScheduleViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<ScheduleDayItem> Days { get; set; }
        public List<WorkShift> Shifts { get; set; }
        public List<Employee> Employees { get; set; }
    }

    public class ScheduleDayItem
    {
        public DateTime Date { get; set; }
        public string DayName { get; set; }
        public bool IsToday { get; set; }
        public bool IsWeekend { get; set; }
        public List<ScheduleSlot> Slots { get; set; }
    }

    public class ScheduleSlot
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public int ShiftId { get; set; }
        public string ShiftName { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Status { get; set; }
    }

    /// <summary>
    /// ViewModel cho thống kê nhân viên
    /// </summary>
    public class EmployeeStatsViewModel
    {
        public Employee Employee { get; set; }
        public EmployeeContract CurrentContract { get; set; }
        public AttendanceSummaryStats AttendanceStats { get; set; }
        public LeaveBalanceStats LeaveStats { get; set; }
        public List<PerformanceReviewItem> RecentReviews { get; set; }
        public List<PayrollItem> RecentPayrolls { get; set; }
    }

    #endregion

    #region Request/Response Models

    public class AttendanceResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Status { get; set; }
        public DateTime? CheckTime { get; set; }
        public decimal? WorkHours { get; set; }
    }

    public class LeaveRequestResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int? RequestId { get; set; }
    }

    public class PayrollResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int ProcessedCount { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class HROperationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }
    }

    #endregion
}
