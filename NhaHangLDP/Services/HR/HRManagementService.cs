using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using NhaHangLDP.Models;

namespace NhaHangLDP.Services.HR
{
    /// <summary>
    /// Service quản lý nhân sự toàn diện
    /// </summary>
    public class HRManagementService
    {
        private readonly MyDbContext _db;

        // Cấu hình mặc định
        private const int ANNUAL_LEAVE_DAYS = 12;
        private const int LATE_THRESHOLD_MINUTES = 15;
        private const decimal LATE_DEDUCTION_AMOUNT = 50000;
        private const decimal OVERTIME_RATE = 1.5m;

        public HRManagementService(MyDbContext context)
        {
            _db = context;
        }

        #region Dashboard

        /// <summary>
        /// Lấy dữ liệu Dashboard HR
        /// </summary>
        public HRDashboardViewModel GetDashboardData()
        {
            var today = DateTime.Today;
            var thisMonth = new DateTime(today.Year, today.Month, 1);
            var lastMonth = thisMonth.AddMonths(-1);

            var employees = _db.Employees.Include(e => e.Role).ToList();
            var activeEmployees = employees.Where(e => e.IsActive).ToList();

            // Thống kê chấm công hôm nay
            var todayAttendance = GetTodayAttendance();
            var presentToday = todayAttendance.Count(a => a.Status != "Absent");
            var lateToday = todayAttendance.Count(a => a.Status == "Late");
            var absentToday = activeEmployees.Count - presentToday;

            // Thống kê đơn xin nghỉ
            var pendingLeaves = GetPendingLeaveRequests().Count;

            // Tính tỷ lệ chuyên cần tháng này
            var attendanceRate = CalculateMonthlyAttendanceRate(thisMonth);

            // Top performers
            var topPerformers = GetTopPerformers(5);

            // Trend chấm công 7 ngày gần nhất
            var attendanceTrend = GetAttendanceTrend(7);

            return new HRDashboardViewModel
            {
                TotalEmployees = employees.Count,
                ActiveEmployees = activeEmployees.Count,
                InactiveEmployees = employees.Count - activeEmployees.Count,
                NewEmployeesThisMonth = employees.Count(e => e.HireDate >= thisMonth),
                AttendanceRate = attendanceRate,
                PendingLeaveRequests = pendingLeaves,
                TodayPresent = presentToday,
                TodayAbsent = absentToday,
                TodayLate = lateToday,
                AverageWorkHours = CalculateAverageWorkHours(thisMonth),
                TodayAttendance = todayAttendance,
                EmployeesByRole = GetEmployeesByRole(),
                RecentLeaveRequests = GetRecentLeaveRequests(5),
                TopPerformers = topPerformers,
                AttendanceTrend = attendanceTrend
            };
        }

        #endregion

        #region Attendance Management

        /// <summary>
        /// Check-in nhân viên
        /// </summary>
        public AttendanceResult CheckIn(int employeeId, string note = null, string location = null)
        {
            try
            {
                var today = DateTime.Today;
                var now = DateTime.Now;

                // Kiểm tra đã check-in chưa
                var existingAttendance = _db.Set<Attendance>()
                    .FirstOrDefault(a => a.EmployeeId == employeeId &&
                                        DbFunctions.TruncateTime(a.CheckInTime) == today);

                if (existingAttendance != null)
                {
                    return new AttendanceResult
                    {
                        Success = false,
                        Message = "Bạn đã check-in hôm nay rồi!",
                        CheckTime = existingAttendance.CheckInTime
                    };
                }

                // Xác định trạng thái (đúng giờ / trễ)
                var shiftStartTime = new TimeSpan(8, 0, 0); // 8:00 AM mặc định
                var lateThreshold = shiftStartTime.Add(TimeSpan.FromMinutes(LATE_THRESHOLD_MINUTES));
                var status = now.TimeOfDay <= lateThreshold ? "OnTime" : "Late";

                var attendance = new Attendance
                {
                    EmployeeId = employeeId,
                    CheckInTime = now,
                    Status = status,
                    Note = note,
                    Location = location
                };

                _db.Set<Attendance>().Add(attendance);
                _db.SaveChanges();

                return new AttendanceResult
                {
                    Success = true,
                    Message = status == "OnTime"
                        ? $"Check-in thành công lúc {now:HH:mm}!"
                        : $"Check-in thành công lúc {now:HH:mm}. Lưu ý: Bạn đã đi trễ!",
                    Status = status,
                    CheckTime = now
                };
            }
            catch (Exception ex)
            {
                return new AttendanceResult
                {
                    Success = false,
                    Message = "Lỗi hệ thống: " + ex.Message
                };
            }
        }

        /// <summary>
        /// Check-out nhân viên
        /// </summary>
        public AttendanceResult CheckOut(int employeeId, string note = null)
        {
            try
            {
                var today = DateTime.Today;
                var now = DateTime.Now;

                var attendance = _db.Set<Attendance>()
                    .FirstOrDefault(a => a.EmployeeId == employeeId &&
                                        DbFunctions.TruncateTime(a.CheckInTime) == today &&
                                        a.CheckOutTime == null);

                if (attendance == null)
                {
                    return new AttendanceResult
                    {
                        Success = false,
                        Message = "Bạn chưa check-in hoặc đã check-out rồi!"
                    };
                }

                attendance.CheckOutTime = now;
                attendance.WorkHours = (decimal)(now - attendance.CheckInTime).TotalHours;

                if (!string.IsNullOrEmpty(note))
                {
                    attendance.Note = string.IsNullOrEmpty(attendance.Note)
                        ? note
                        : attendance.Note + " | " + note;
                }

                // Kiểm tra về sớm
                var shiftEndTime = new TimeSpan(17, 0, 0); // 5:00 PM mặc định
                if (now.TimeOfDay < shiftEndTime.Subtract(TimeSpan.FromMinutes(30)))
                {
                    attendance.Status = attendance.Status == "Late" ? "Late" : "EarlyLeave";
                }

                _db.SaveChanges();

                return new AttendanceResult
                {
                    Success = true,
                    Message = $"Check-out thành công! Tổng giờ làm: {attendance.WorkHours:F2} giờ",
                    CheckTime = now,
                    WorkHours = attendance.WorkHours
                };
            }
            catch (Exception ex)
            {
                return new AttendanceResult
                {
                    Success = false,
                    Message = "Lỗi hệ thống: " + ex.Message
                };
            }
        }

        /// <summary>
        /// Lấy danh sách chấm công hôm nay
        /// </summary>
        public List<AttendanceSummaryItem> GetTodayAttendance()
        {
            var today = DateTime.Today;
            var activeEmployees = _db.Employees
                .Include(e => e.Role)
                .Where(e => e.IsActive)
                .ToList();

            var todayRecords = _db.Set<Attendance>()
                .Where(a => DbFunctions.TruncateTime(a.CheckInTime) == today)
                .ToList();

            var result = new List<AttendanceSummaryItem>();

            foreach (var emp in activeEmployees)
            {
                var record = todayRecords.FirstOrDefault(r => r.EmployeeId == emp.Id);

                result.Add(new AttendanceSummaryItem
                {
                    EmployeeId = emp.Id,
                    EmployeeName = emp.FullName,
                    RoleName = emp.Role?.RoleName ?? "N/A",
                    CheckInTime = record?.CheckInTime,
                    CheckOutTime = record?.CheckOutTime,
                    WorkHours = record?.WorkHours,
                    Status = record?.Status ?? "NotCheckedIn",
                    StatusClass = GetStatusClass(record?.Status ?? "NotCheckedIn")
                });
            }

            return result.OrderByDescending(r => r.CheckInTime.HasValue)
                        .ThenBy(r => r.EmployeeName)
                        .ToList();
        }

        /// <summary>
        /// Lấy báo cáo chấm công theo khoảng thời gian
        /// </summary>
        public AttendanceReportViewModel GetAttendanceReport(DateTime startDate, DateTime endDate, int? employeeId = null)
        {
            var query = _db.Set<Attendance>()
                .Include(a => a.Employee)
                .Include(a => a.Employee.Role)
                .Where(a => DbFunctions.TruncateTime(a.CheckInTime) >= startDate &&
                           DbFunctions.TruncateTime(a.CheckInTime) <= endDate);

            if (employeeId.HasValue)
            {
                query = query.Where(a => a.EmployeeId == employeeId.Value);
            }

            var records = query.OrderByDescending(a => a.CheckInTime).ToList();

            var reportItems = records.Select(r => new AttendanceReportItem
            {
                Id = r.Id,
                EmployeeId = r.EmployeeId,
                EmployeeName = r.Employee?.FullName ?? "N/A",
                RoleName = r.Employee?.Role?.RoleName ?? "N/A",
                Date = r.CheckInTime.Date,
                CheckInTime = r.CheckInTime,
                CheckOutTime = r.CheckOutTime,
                WorkHours = r.WorkHours,
                Status = r.Status,
                Note = r.Note
            }).ToList();

            // Tính summary
            var totalDays = (endDate - startDate).Days + 1;
            var presentDays = records.Select(r => r.CheckInTime.Date).Distinct().Count();

            return new AttendanceReportViewModel
            {
                StartDate = startDate,
                EndDate = endDate,
                EmployeeId = employeeId,
                AttendanceRecords = reportItems,
                Summary = new AttendanceSummaryStats
                {
                    TotalDays = totalDays,
                    PresentDays = presentDays,
                    LateDays = records.Count(r => r.Status == "Late"),
                    AbsentDays = totalDays - presentDays,
                    TotalWorkHours = records.Sum(r => r.WorkHours ?? 0),
                    AverageWorkHours = presentDays > 0 ? records.Sum(r => r.WorkHours ?? 0) / presentDays : 0,
                    AttendanceRate = totalDays > 0 ? (decimal)presentDays / totalDays * 100 : 0
                },
                Employees = _db.Employees.Where(e => e.IsActive).ToList()
            };
        }

        /// <summary>
        /// Chấm công thủ công (admin)
        /// </summary>
        public AttendanceResult ManualAttendance(int employeeId, DateTime checkIn, DateTime? checkOut, string status, string note)
        {
            try
            {
                var date = checkIn.Date;
                var existing = _db.Set<Attendance>()
                    .FirstOrDefault(a => a.EmployeeId == employeeId &&
                                        DbFunctions.TruncateTime(a.CheckInTime) == date);

                if (existing != null)
                {
                    // Cập nhật
                    existing.CheckInTime = checkIn;
                    existing.CheckOutTime = checkOut;
                    existing.Status = status;
                    existing.Note = note;
                    if (checkOut.HasValue)
                    {
                        existing.WorkHours = (decimal)(checkOut.Value - checkIn).TotalHours;
                    }
                }
                else
                {
                    // Tạo mới
                    var attendance = new Attendance
                    {
                        EmployeeId = employeeId,
                        CheckInTime = checkIn,
                        CheckOutTime = checkOut,
                        Status = status,
                        Note = note,
                        WorkHours = checkOut.HasValue ? (decimal)(checkOut.Value - checkIn).TotalHours : (decimal?)null
                    };
                    _db.Set<Attendance>().Add(attendance);
                }

                _db.SaveChanges();

                return new AttendanceResult
                {
                    Success = true,
                    Message = "Cập nhật chấm công thành công!"
                };
            }
            catch (Exception ex)
            {
                return new AttendanceResult
                {
                    Success = false,
                    Message = "Lỗi: " + ex.Message
                };
            }
        }

        #endregion

        #region Leave Management

        /// <summary>
        /// Tạo đơn xin nghỉ phép
        /// </summary>
        public LeaveRequestResult SubmitLeaveRequest(LeaveRequest request)
        {
            try
            {
                // Validate
                if (request.EndDate < request.StartDate)
                {
                    return new LeaveRequestResult
                    {
                        Success = false,
                        Message = "Ngày kết thúc phải sau ngày bắt đầu!"
                    };
                }

                var requestedDays = (request.EndDate - request.StartDate).Days + 1;

                // Kiểm tra số ngày phép còn lại (nếu là nghỉ phép năm)
                if (request.LeaveType == "Annual")
                {
                    var usedDays = GetUsedAnnualLeaveDays(request.EmployeeId, request.StartDate.Year);
                    if (usedDays + requestedDays > ANNUAL_LEAVE_DAYS)
                    {
                        return new LeaveRequestResult
                        {
                            Success = false,
                            Message = $"Bạn đã sử dụng {usedDays}/{ANNUAL_LEAVE_DAYS} ngày phép năm. Không đủ ngày phép!"
                        };
                    }
                }

                // Kiểm tra trùng lịch
                var hasOverlap = _db.Set<LeaveRequest>()
                    .Any(l => l.EmployeeId == request.EmployeeId &&
                             l.Status != "Rejected" &&
                             l.Status != "Cancelled" &&
                             ((request.StartDate >= l.StartDate && request.StartDate <= l.EndDate) ||
                              (request.EndDate >= l.StartDate && request.EndDate <= l.EndDate)));

                if (hasOverlap)
                {
                    return new LeaveRequestResult
                    {
                        Success = false,
                        Message = "Bạn đã có đơn xin nghỉ trong khoảng thời gian này!"
                    };
                }

                request.Status = "Pending";
                request.CreatedDate = DateTime.Now;

                _db.Set<LeaveRequest>().Add(request);
                _db.SaveChanges();

                return new LeaveRequestResult
                {
                    Success = true,
                    Message = "Gửi đơn xin nghỉ thành công! Đang chờ phê duyệt.",
                    RequestId = request.Id
                };
            }
            catch (Exception ex)
            {
                return new LeaveRequestResult
                {
                    Success = false,
                    Message = "Lỗi: " + ex.Message
                };
            }
        }

        /// <summary>
        /// Phê duyệt đơn xin nghỉ
        /// </summary>
        public LeaveRequestResult ApproveLeaveRequest(int requestId, int approverId)
        {
            try
            {
                var request = _db.Set<LeaveRequest>().Find(requestId);
                if (request == null)
                {
                    return new LeaveRequestResult
                    {
                        Success = false,
                        Message = "Không tìm thấy đơn xin nghỉ!"
                    };
                }

                if (request.Status != "Pending")
                {
                    return new LeaveRequestResult
                    {
                        Success = false,
                        Message = "Đơn này đã được xử lý!"
                    };
                }

                request.Status = "Approved";
                request.ApprovedBy = approverId;
                request.ApprovedDate = DateTime.Now;

                _db.SaveChanges();

                return new LeaveRequestResult
                {
                    Success = true,
                    Message = "Đã phê duyệt đơn xin nghỉ!"
                };
            }
            catch (Exception ex)
            {
                return new LeaveRequestResult
                {
                    Success = false,
                    Message = "Lỗi: " + ex.Message
                };
            }
        }

        /// <summary>
        /// Từ chối đơn xin nghỉ
        /// </summary>
        public LeaveRequestResult RejectLeaveRequest(int requestId, int approverId, string reason = null)
        {
            try
            {
                var request = _db.Set<LeaveRequest>().Find(requestId);
                if (request == null)
                {
                    return new LeaveRequestResult
                    {
                        Success = false,
                        Message = "Không tìm thấy đơn xin nghỉ!"
                    };
                }

                request.Status = "Rejected";
                request.ApprovedBy = approverId;
                request.ApprovedDate = DateTime.Now;
                if (!string.IsNullOrEmpty(reason))
                {
                    request.Reason = request.Reason + " | Lý do từ chối: " + reason;
                }

                _db.SaveChanges();

                return new LeaveRequestResult
                {
                    Success = true,
                    Message = "Đã từ chối đơn xin nghỉ!"
                };
            }
            catch (Exception ex)
            {
                return new LeaveRequestResult
                {
                    Success = false,
                    Message = "Lỗi: " + ex.Message
                };
            }
        }

        /// <summary>
        /// Lấy danh sách đơn xin nghỉ chờ duyệt
        /// </summary>
        public List<LeaveRequestItem> GetPendingLeaveRequests()
        {
            return _db.Set<LeaveRequest>()
                .Include(l => l.Employee)
                .Where(l => l.Status == "Pending")
                .OrderBy(l => l.StartDate)
                .ToList()
                .Select(l => MapToLeaveRequestItem(l))
                .ToList();
        }

        /// <summary>
        /// Lấy đơn xin nghỉ gần đây
        /// </summary>
        public List<LeaveRequestItem> GetRecentLeaveRequests(int count)
        {
            return _db.Set<LeaveRequest>()
                .Include(l => l.Employee)
                .OrderByDescending(l => l.CreatedDate)
                .Take(count)
                .ToList()
                .Select(l => MapToLeaveRequestItem(l))
                .ToList();
        }

        /// <summary>
        /// Lấy thống kê nghỉ phép của nhân viên
        /// </summary>
        public LeaveBalanceStats GetLeaveBalance(int employeeId, int year)
        {
            var leaves = _db.Set<LeaveRequest>()
                .Where(l => l.EmployeeId == employeeId &&
                           l.Status == "Approved" &&
                           l.StartDate.Year == year)
                .ToList();

            var annualUsed = leaves.Where(l => l.LeaveType == "Annual")
                .Sum(l => (l.EndDate - l.StartDate).Days + 1);

            return new LeaveBalanceStats
            {
                TotalAnnualLeave = ANNUAL_LEAVE_DAYS,
                UsedAnnualLeave = annualUsed,
                RemainingAnnualLeave = ANNUAL_LEAVE_DAYS - annualUsed,
                SickLeaveUsed = leaves.Where(l => l.LeaveType == "Sick")
                    .Sum(l => (l.EndDate - l.StartDate).Days + 1),
                UnpaidLeaveUsed = leaves.Where(l => l.LeaveType == "Unpaid")
                    .Sum(l => (l.EndDate - l.StartDate).Days + 1)
            };
        }

        #endregion

        #region Payroll Management

        /// <summary>
        /// Tính lương tháng cho tất cả nhân viên
        /// </summary>
        public PayrollResult CalculateMonthlyPayroll(int month, int year)
        {
            try
            {
                var startDate = new DateTime(year, month, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);

                // Lấy nhân viên có hợp đồng active
                var employees = _db.Employees
                    .Include(e => e.Role)
                    .Where(e => e.IsActive)
                    .ToList();

                var contracts = _db.Set<EmployeeContract>()
                    .Where(c => c.Status == "Active")
                    .ToList();

                var payrolls = new List<Payroll>();
                var workDays = GetWorkDays(month, year);

                foreach (var emp in employees)
                {
                    var contract = contracts.FirstOrDefault(c => c.EmployeeId == emp.Id);
                    if (contract == null) continue;

                    // Kiểm tra đã tính lương chưa
                    var existingPayroll = _db.Set<Payroll>()
                        .FirstOrDefault(p => p.EmployeeId == emp.Id && p.Month == month && p.Year == year);

                    if (existingPayroll != null) continue;

                    // Lấy dữ liệu chấm công
                    var attendance = _db.Set<Attendance>()
                        .Where(a => a.EmployeeId == emp.Id &&
                                   DbFunctions.TruncateTime(a.CheckInTime) >= startDate &&
                                   DbFunctions.TruncateTime(a.CheckInTime) <= endDate)
                        .ToList();

                    var presentDays = attendance.Count;
                    var lateCount = attendance.Count(a => a.Status == "Late");
                    var totalHours = attendance.Sum(a => a.WorkHours ?? 0);

                    // Tính lương
                    var baseSalary = contract.BaseSalary;
                    var allowance = contract.Allowance ?? 0;
                    var dailyRate = baseSalary / workDays;

                    // Overtime (trên 8 giờ/ngày)
                    var standardHours = presentDays * 8;
                    var overtimeHours = totalHours - standardHours;
                    var overtimeBonus = overtimeHours > 0 ? (baseSalary / workDays / 8) * overtimeHours * OVERTIME_RATE : 0;

                    // Performance bonus (từ đánh giá gần nhất)
                    var performanceBonus = GetPerformanceBonus(emp.Id, month, year);

                    // Khấu trừ
                    var lateDeduction = lateCount * LATE_DEDUCTION_AMOUNT;
                    var absentDays = workDays - presentDays;
                    var absentDeduction = absentDays * dailyRate;
                    var totalDeduction = lateDeduction + absentDeduction;

                    var netSalary = baseSalary + allowance + overtimeBonus + performanceBonus - totalDeduction;

                    var payroll = new Payroll
                    {
                        EmployeeId = emp.Id,
                        Month = month,
                        Year = year,
                        BaseSalary = baseSalary,
                        Allowance = allowance,
                        OvertimeBonus = overtimeBonus,
                        PerformanceBonus = performanceBonus,
                        Deduction = totalDeduction,
                        TotalWorkDays = presentDays,
                        TotalWorkHours = totalHours,
                        LateCount = lateCount,
                        AbsentCount = absentDays,
                        NetSalary = netSalary,
                        Status = "Draft",
                        CreatedDate = DateTime.Now
                    };

                    payrolls.Add(payroll);
                }

                if (payrolls.Any())
                {
                    _db.Set<Payroll>().AddRange(payrolls);
                    _db.SaveChanges();
                }

                return new PayrollResult
                {
                    Success = true,
                    Message = $"Đã tính lương cho {payrolls.Count} nhân viên!",
                    ProcessedCount = payrolls.Count,
                    TotalAmount = payrolls.Sum(p => p.NetSalary)
                };
            }
            catch (Exception ex)
            {
                return new PayrollResult
                {
                    Success = false,
                    Message = "Lỗi: " + ex.Message
                };
            }
        }

        /// <summary>
        /// Lấy danh sách bảng lương theo tháng
        /// </summary>
        public PayrollViewModel GetPayrollByMonth(int month, int year)
        {
            var payrolls = _db.Set<Payroll>()
                .Include(p => p.Employee)
                .Include(p => p.Employee.Role)
                .Where(p => p.Month == month && p.Year == year)
                .ToList();

            var items = payrolls.Select(p => new PayrollItem
            {
                Id = p.Id,
                EmployeeId = p.EmployeeId,
                EmployeeName = p.Employee?.FullName ?? "N/A",
                RoleName = p.Employee?.Role?.RoleName ?? "N/A",
                BaseSalary = p.BaseSalary,
                Allowance = p.Allowance,
                OvertimeBonus = p.OvertimeBonus,
                PerformanceBonus = p.PerformanceBonus,
                TotalBonus = p.OvertimeBonus + p.PerformanceBonus,
                Deduction = p.Deduction,
                NetSalary = p.NetSalary,
                TotalWorkDays = p.TotalWorkDays,
                TotalWorkHours = p.TotalWorkHours,
                LateCount = p.LateCount,
                AbsentCount = p.AbsentCount,
                Status = p.Status,
                StatusClass = GetPayrollStatusClass(p.Status)
            }).ToList();

            return new PayrollViewModel
            {
                Month = month,
                Year = year,
                PayrollItems = items,
                Summary = new PayrollSummary
                {
                    TotalEmployees = items.Count,
                    TotalBaseSalary = items.Sum(i => i.BaseSalary),
                    TotalBonus = items.Sum(i => i.TotalBonus),
                    TotalDeduction = items.Sum(i => i.Deduction),
                    TotalNetSalary = items.Sum(i => i.NetSalary),
                    PaidCount = items.Count(i => i.Status == "Paid"),
                    PendingCount = items.Count(i => i.Status != "Paid")
                }
            };
        }

        /// <summary>
        /// Duyệt bảng lương
        /// </summary>
        public HROperationResult ApprovePayroll(int payrollId)
        {
            try
            {
                var payroll = _db.Set<Payroll>().Find(payrollId);
                if (payroll == null)
                {
                    return new HROperationResult { Success = false, Message = "Không tìm thấy bảng lương!" };
                }

                payroll.Status = "Approved";
                _db.SaveChanges();

                return new HROperationResult { Success = true, Message = "Đã duyệt bảng lương!" };
            }
            catch (Exception ex)
            {
                return new HROperationResult { Success = false, Message = "Lỗi: " + ex.Message };
            }
        }

        /// <summary>
        /// Xác nhận thanh toán lương
        /// </summary>
        public HROperationResult MarkPayrollAsPaid(int payrollId)
        {
            try
            {
                var payroll = _db.Set<Payroll>().Find(payrollId);
                if (payroll == null)
                {
                    return new HROperationResult { Success = false, Message = "Không tìm thấy bảng lương!" };
                }

                payroll.Status = "Paid";
                payroll.PaymentDate = DateTime.Now;
                _db.SaveChanges();

                return new HROperationResult { Success = true, Message = "Đã xác nhận thanh toán lương!" };
            }
            catch (Exception ex)
            {
                return new HROperationResult { Success = false, Message = "Lỗi: " + ex.Message };
            }
        }

        #endregion

        #region Performance Review

        /// <summary>
        /// Tạo đánh giá hiệu suất
        /// </summary>
        public HROperationResult CreatePerformanceReview(PerformanceReview review)
        {
            try
            {
                // Tính điểm tổng
                review.OverallScore = (review.ServiceQuality + review.Punctuality +
                    review.Teamwork + review.Communication + review.WorkEfficiency) / 5.0m;
                review.CreatedDate = DateTime.Now;

                _db.Set<PerformanceReview>().Add(review);
                _db.SaveChanges();

                return new HROperationResult
                {
                    Success = true,
                    Message = "Đã tạo đánh giá hiệu suất thành công!"
                };
            }
            catch (Exception ex)
            {
                return new HROperationResult
                {
                    Success = false,
                    Message = "Lỗi: " + ex.Message
                };
            }
        }

        /// <summary>
        /// Lấy danh sách đánh giá
        /// </summary>
        public PerformanceReviewViewModel GetPerformanceReviews(int? employeeId = null, string period = null)
        {
            var query = _db.Set<PerformanceReview>()
                .Include(r => r.Employee)
                .Include(r => r.Employee.Role)
                .AsQueryable();

            if (employeeId.HasValue)
            {
                query = query.Where(r => r.EmployeeId == employeeId.Value);
            }

            if (!string.IsNullOrEmpty(period))
            {
                query = query.Where(r => r.ReviewPeriod == period);
            }

            var reviews = query.OrderByDescending(r => r.ReviewDate).ToList();

            var items = reviews.Select(r => new PerformanceReviewItem
            {
                Id = r.Id,
                EmployeeId = r.EmployeeId,
                EmployeeName = r.Employee?.FullName ?? "N/A",
                RoleName = r.Employee?.Role?.RoleName ?? "N/A",
                ReviewerName = GetEmployeeName(r.ReviewerId),
                ReviewDate = r.ReviewDate,
                ReviewPeriod = r.ReviewPeriod,
                ServiceQuality = r.ServiceQuality,
                Punctuality = r.Punctuality,
                Teamwork = r.Teamwork,
                Communication = r.Communication,
                WorkEfficiency = r.WorkEfficiency,
                OverallScore = r.OverallScore ?? 0,
                PerformanceLevel = GetPerformanceLevel(r.OverallScore ?? 0),
                Comments = r.Comments
            }).ToList();

            return new PerformanceReviewViewModel
            {
                Reviews = items,
                Summary = new PerformanceSummary
                {
                    AverageScore = items.Any() ? items.Average(i => i.OverallScore) : 0,
                    ExcellentCount = items.Count(i => i.OverallScore >= 4.5m),
                    GoodCount = items.Count(i => i.OverallScore >= 3.5m && i.OverallScore < 4.5m),
                    AverageCount = items.Count(i => i.OverallScore >= 2.5m && i.OverallScore < 3.5m),
                    BelowAverageCount = items.Count(i => i.OverallScore >= 1.5m && i.OverallScore < 2.5m),
                    PoorCount = items.Count(i => i.OverallScore < 1.5m)
                }
            };
        }

        /// <summary>
        /// Lấy top performers
        /// </summary>
        public List<TopPerformerItem> GetTopPerformers(int count)
        {
            var thisYear = DateTime.Now.Year;

            return _db.Set<PerformanceReview>()
                .Include(r => r.Employee)
                .Include(r => r.Employee.Role)
                .Where(r => r.ReviewDate.Year == thisYear)
                .GroupBy(r => new { r.EmployeeId, r.ReportedByEmployee?.FullName, RoleName = r.Employee.Role.RoleName })
                .Select(g => new
                {
                    EmployeeId = g.Key.EmployeeId,
                    EmployeeName = g.Key.FullName,
                    RoleName = g.Key.RoleName,
                    AvgScore = g.Average(r => r.OverallScore),
                    ReviewCount = g.Count()
                })
                .OrderByDescending(x => x.AvgScore)
                .Take(count)
                .ToList()
                .Select(x => new TopPerformerItem
                {
                    EmployeeId = x.EmployeeId,
                    EmployeeName = x.EmployeeName,
                    RoleName = x.RoleName,
                    OverallScore = x.AvgScore ?? 0,
                    ReviewCount = x.ReviewCount
                })
                .ToList();
        }

        #endregion

        #region Employee Contract

        /// <summary>
        /// Tạo hợp đồng lao động
        /// </summary>
        public HROperationResult CreateContract(EmployeeContract contract)
        {
            try
            {
                // Kiểm tra hợp đồng active hiện tại
                var existingActive = _db.Set<EmployeeContract>()
                    .FirstOrDefault(c => c.EmployeeId == contract.EmployeeId && c.Status == "Active");

                if (existingActive != null)
                {
                    existingActive.Status = "Expired";
                }

                contract.Status = "Active";
                contract.CreatedDate = DateTime.Now;

                _db.Set<EmployeeContract>().Add(contract);
                _db.SaveChanges();

                return new HROperationResult
                {
                    Success = true,
                    Message = "Đã tạo hợp đồng thành công!"
                };
            }
            catch (Exception ex)
            {
                return new HROperationResult
                {
                    Success = false,
                    Message = "Lỗi: " + ex.Message
                };
            }
        }

        /// <summary>
        /// Lấy hợp đồng hiện tại của nhân viên
        /// </summary>
        public EmployeeContract GetActiveContract(int employeeId)
        {
            return _db.Set<EmployeeContract>()
                .FirstOrDefault(c => c.EmployeeId == employeeId && c.Status == "Active");
        }

        /// <summary>
        /// Lấy danh sách hợp đồng
        /// </summary>
        public List<EmployeeContract> GetAllContracts()
        {
            return _db.EmployeeContract
                .Include(c => c.Employee)
                .Include(c => c.Employee.Role)
                .OrderByDescending(c => c.CreatedDate)
                .ToList();
        }

        #endregion

        #region Work Schedule

        /// <summary>
        /// Tạo ca làm việc
        /// </summary>
        public HROperationResult CreateWorkShift(WorkShift shift)
        {
            try
            {
                shift.WorkHours = (decimal)(shift.EndTime - shift.StartTime).TotalHours;
                _db.Set<WorkShift>().Add(shift);
                _db.SaveChanges();

                return new HROperationResult { Success = true, Message = "Đã tạo ca làm việc!" };
            }
            catch (Exception ex)
            {
                return new HROperationResult { Success = false, Message = "Lỗi: " + ex.Message };
            }
        }

        /// <summary>
        /// Lấy danh sách ca làm việc
        /// </summary>
        public List<WorkShift> GetAllWorkShifts()
        {
            return _db.Set<WorkShift>().Where(s => s.IsActive).ToList();
        }

        /// <summary>
        /// Xếp lịch làm việc
        /// </summary>
        public HROperationResult AssignSchedule(int employeeId, int shiftId, DateTime workDate)
        {
            try
            {
                var existing = _db.Set<EmployeeSchedule>()
                    .FirstOrDefault(s => s.EmployeeId == employeeId && s.WorkDate == workDate);

                if (existing != null)
                {
                    existing.WorkShiftId = shiftId;
                    existing.Status = "Scheduled";
                }
                else
                {
                    _db.Set<EmployeeSchedule>().Add(new EmployeeSchedule
                    {
                        EmployeeId = employeeId,
                        WorkShiftId = shiftId,
                        WorkDate = workDate,
                        Status = "Scheduled"
                    });
                }

                _db.SaveChanges();

                return new HROperationResult { Success = true, Message = "Đã xếp lịch thành công!" };
            }
            catch (Exception ex)
            {
                return new HROperationResult { Success = false, Message = "Lỗi: " + ex.Message };
            }
        }

        /// <summary>
        /// Lấy lịch làm việc theo tuần
        /// </summary>
        public ScheduleViewModel GetWeeklySchedule(DateTime weekStart)
        {
            var weekEnd = weekStart.AddDays(6);

            var schedules = _db.Set<EmployeeSchedule>()
                .Include(s => s.Cashier)
                .Include(s => s.WorkShift)
                .Where(s => s.WorkDate >= weekStart && s.WorkDate <= weekEnd)
                .ToList();

            var shifts = GetAllWorkShifts();
            var employees = _db.Employees.Where(e => e.IsActive).ToList();

            var days = new List<ScheduleDayItem>();
            for (int i = 0; i < 7; i++)
            {
                var date = weekStart.AddDays(i);
                var daySchedules = schedules.Where(s => s.WorkDate == date).ToList();

                days.Add(new ScheduleDayItem
                {
                    Date = date,
                    DayName = date.ToString("dddd", new System.Globalization.CultureInfo("vi-VN")),
                    IsToday = date == DateTime.Today,
                    IsWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday,
                    Slots = daySchedules.Select(s => new ScheduleSlot
                    {
                        EmployeeId = s.EmployeeId,
                        EmployeeName = s.Cashier?.FullName ?? "N/A",
                        ShiftId = s.WorkShiftId,
                        ShiftName = s.WorkShift?.ShiftName ?? "N/A",
                        StartTime = s.WorkShift?.StartTime ?? TimeSpan.Zero,
                        EndTime = s.WorkShift?.EndTime ?? TimeSpan.Zero,
                        Status = s.Status
                    }).ToList()
                });
            }

            return new ScheduleViewModel
            {
                StartDate = weekStart,
                EndDate = weekEnd,
                Days = days,
                Shifts = shifts,
                Employees = employees
            };
        }

        #endregion

        #region Statistics & Analytics

        /// <summary>
        /// Thống kê nhân viên theo vai trò
        /// </summary>
        public List<EmployeeByRoleItem> GetEmployeesByRole()
        {
            var colors = new[] { "#3498db", "#e74c3c", "#2ecc71", "#f39c12", "#9b59b6", "#1abc9c" };
            var index = 0;

            return _db.Employees
                .Include(e => e.Role)
                .Where(e => e.IsActive)
                .GroupBy(e => e.Role.RoleName)
                .Select(g => new { RoleName = g.Key, Count = g.Count() })
                .ToList()
                .Select(x => new EmployeeByRoleItem
                {
                    RoleName = x.RoleName,
                    Count = x.Count,
                    Color = colors[index++ % colors.Length]
                })
                .ToList();
        }

        /// <summary>
        /// Trend chấm công
        /// </summary>
        public List<AttendanceTrendItem> GetAttendanceTrend(int days)
        {
            var result = new List<AttendanceTrendItem>();
            var activeCount = _db.Employees.Count(e => e.IsActive);

            for (int i = days - 1; i >= 0; i--)
            {
                var date = DateTime.Today.AddDays(-i);
                var records = _db.Set<Attendance>()
                    .Where(a => DbFunctions.TruncateTime(a.CheckInTime) == date)
                    .ToList();

                var present = records.Count;
                var late = records.Count(r => r.Status == "Late");
                var absent = activeCount - present;

                result.Add(new AttendanceTrendItem
                {
                    Label = date.ToString("dd/MM"),
                    Present = present,
                    Late = late,
                    Absent = absent,
                    AttendanceRate = activeCount > 0 ? (decimal)present / activeCount * 100 : 0
                });
            }

            return result;
        }

        #endregion

        #region Helper Methods

        private string GetEmployeeName(int employeeId)
        {
            var employee = _db.Employees.Find(employeeId);
            return employee?.FullName ?? "N/A";
        }

        private int GetUsedAnnualLeaveDays(int employeeId, int year)
        {
            return _db.Set<LeaveRequest>()
                .Where(l => l.EmployeeId == employeeId &&
                           l.LeaveType == "Annual" &&
                           l.Status == "Approved" &&
                           l.StartDate.Year == year)
                .ToList()
                .Sum(l => (l.EndDate - l.StartDate).Days + 1);
        }

        private decimal CalculateMonthlyAttendanceRate(DateTime month)
        {
            var startDate = new DateTime(month.Year, month.Month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);
            var workDays = GetWorkDays(month.Month, month.Year);
            var activeEmployees = _db.Employees.Count(e => e.IsActive);

            if (activeEmployees == 0 || workDays == 0) return 0;

            var presentDays = _db.Set<Attendance>()
                .Where(a => DbFunctions.TruncateTime(a.CheckInTime) >= startDate &&
                           DbFunctions.TruncateTime(a.CheckInTime) <= endDate)
                .Count();

            var expectedDays = activeEmployees * workDays;
            return expectedDays > 0 ? (decimal)presentDays / expectedDays * 100 : 0;
        }

        private decimal CalculateAverageWorkHours(DateTime month)
        {
            var startDate = new DateTime(month.Year, month.Month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            var records = _db.Set<Attendance>()
                .Where(a => DbFunctions.TruncateTime(a.CheckInTime) >= startDate &&
                           DbFunctions.TruncateTime(a.CheckInTime) <= endDate &&
                           a.WorkHours != null)
                .ToList();

            return records.Any() ? records.Average(r => r.WorkHours ?? 0) : 0;
        }

        private int GetWorkDays(int month, int year)
        {
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);
            var workDays = 0;

            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
                {
                    workDays++;
                }
            }

            return workDays;
        }

        private decimal GetPerformanceBonus(int employeeId, int month, int year)
        {
            var latestReview = _db.Set<PerformanceReview>()
                .Where(r => r.EmployeeId == employeeId && r.ReviewDate.Year == year)
                .OrderByDescending(r => r.ReviewDate)
                .FirstOrDefault();

            if (latestReview == null) return 0;

            // Bonus dựa trên điểm đánh giá
            if (latestReview.OverallScore >= 4.5m) return 500000; // Xuất sắc
            if (latestReview.OverallScore >= 3.5m) return 300000; // Tốt
            if (latestReview.OverallScore >= 2.5m) return 100000; // Trung bình
            return 0;
        }

        private string GetStatusClass(string status)
        {
            switch (status)
            {
                case "OnTime": return "success";
                case "Late": return "warning";
                case "EarlyLeave": return "info";
                case "Absent": return "danger";
                case "NotCheckedIn": return "secondary";
                default: return "secondary";
            }
        }

        private string GetPayrollStatusClass(string status)
        {
            switch (status)
            {
                case "Paid": return "success";
                case "Approved": return "info";
                case "Draft": return "warning";
                default: return "secondary";
            }
        }

        private string GetPerformanceLevel(decimal score)
        {
            if (score >= 4.5m) return "Xuất sắc";
            if (score >= 3.5m) return "Tốt";
            if (score >= 2.5m) return "Trung bình";
            if (score >= 1.5m) return "Dưới trung bình";
            return "Yếu";
        }

        private LeaveRequestItem MapToLeaveRequestItem(LeaveRequest l)
        {
            return new LeaveRequestItem
            {
                Id = l.Id,
                EmployeeName = l.Employee?.FullName ?? "N/A",
                LeaveType = l.LeaveType,
                StartDate = l.StartDate,
                EndDate = l.EndDate,
                Days = (l.EndDate - l.StartDate).Days + 1,
                Status = l.Status,
                StatusClass = GetLeaveStatusClass(l.Status),
                Reason = l.Reason
            };
        }

        private string GetLeaveStatusClass(string status)
        {
            switch (status)
            {
                case "Approved": return "success";
                case "Pending": return "warning";
                case "Rejected": return "danger";
                case "Cancelled": return "secondary";
                default: return "secondary";
            }
        }

        #endregion
    }
}
