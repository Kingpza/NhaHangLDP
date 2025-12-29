using NhaHangLDP.Helpers;
using NhaHangLDP.Models;
using NhaHangLDP.Services.HR;
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace NhaHangLDP.Controllers
{
    /// <summary>
    /// Controller quản lý nhân sự
    /// </summary>
    public class HRController : Controller
    {
        private readonly MyDbContext _db = new MyDbContext();
        private readonly HRManagementService _hrService;

        public HRController()
        {
            _hrService = new HRManagementService(_db);
        }

        #region Authorization

        private bool IsAuthorized()
        {
            return AuthorizationHelper.IsAdminOrManager(HttpContext.Session);
        }

        private ActionResult RedirectUnauthorized()
        {
            return RedirectToAction("Login", "Account");
        }

        private int GetCurrentEmployeeId()
        {
            var employeeIdStr = HttpContext.Session.GetString("EmployeeId");
            if (!string.IsNullOrEmpty(employeeIdStr) && int.TryParse(employeeIdStr, out int employeeId))
                return employeeId;

            var adminEmployee = _db.Employees.FirstOrDefault(e => e.Role.RoleName == "Admin" && e.IsActive);
            return adminEmployee?.Id ?? 1;
        }

        #endregion

        #region Dashboard

        /// <summary>
        /// Dashboard tổng quan HR
        /// </summary>
        public ActionResult Dashboard()
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            try
            {
                // Kiểm tra xem các tables HR đã tồn tại chưa
                if (!IsHRTablesExist())
                {
                    ViewBag.NeedSetup = true;
                    ViewBag.SetupMessage = "Các bảng dữ liệu HR chưa được tạo. Vui lòng click nút 'Khởi tạo Database HR' để thiết lập.";
                    return View(new HRDashboardViewModel());
                }

                var viewModel = _hrService.GetDashboardData();
                return View(viewModel);
            }
            catch (Exception ex)
            {
                // Nếu lỗi do tables chưa tồn tại
                if (ex.Message.Contains("Invalid object name") || ex.InnerException?.Message?.Contains("Invalid object name") == true)
                {
                    ViewBag.NeedSetup = true;
                    ViewBag.SetupMessage = "Các bảng dữ liệu HR chưa được tạo. Vui lòng click nút 'Khởi tạo Database HR' để thiết lập.";
                    return View(new HRDashboardViewModel());
                }
                
                TempData["Error"] = "Lỗi khi tải Dashboard: " + ex.Message;
                return View(new HRDashboardViewModel());
            }
        }

        /// <summary>
        /// API lấy dữ liệu Dashboard
        /// </summary>
        [HttpGet]
        public JsonResult GetDashboardData()
        {
            try
            {
                var data = _hrService.GetDashboardData();
                return Json(new { success = true, data = data });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region Attendance

        /// <summary>
        /// Trang chấm công
        /// </summary>
        public ActionResult Attendance()
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            var todayAttendance = _hrService.GetTodayAttendance();
            ViewBag.Employees = _db.Employees
                .Include(e => e.Role)
                .Where(e => e.IsActive)
                .OrderBy(e => e.FullName)
                .ToList();
            return View(todayAttendance);
        }

        /// <summary>
        /// Check-in
        /// </summary>
        [HttpPost]
        public JsonResult CheckIn(int employeeId, string note = null)
        {
            var result = _hrService.CheckIn(employeeId, note);
            return Json(result);
        }

        /// <summary>
        /// Check-out
        /// </summary>
        [HttpPost]
        public JsonResult CheckOut(int employeeId, string note = null)
        {
            var result = _hrService.CheckOut(employeeId, note);
            return Json(result);
        }

        /// <summary>
        /// Chấm công thủ công (Admin)
        /// </summary>
        [HttpPost]
        public JsonResult ManualAttendance(int employeeId, DateTime checkIn, DateTime? checkOut, string status, string note)
        {
            if (!IsAuthorized())
                return Json(new { success = false, message = "Không có quyền!" });

            var result = _hrService.ManualAttendance(employeeId, checkIn, checkOut, status, note);
            return Json(result);
        }

        /// <summary>
        /// Báo cáo chấm công
        /// </summary>
        public ActionResult AttendanceReport(DateTime? startDate = null, DateTime? endDate = null, int? employeeId = null)
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            var start = startDate ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var end = endDate ?? DateTime.Today;

            var viewModel = _hrService.GetAttendanceReport(start, end, employeeId);
            return View(viewModel);
        }

        /// <summary>
        /// API lấy chấm công hôm nay
        /// </summary>
        [HttpGet]
        public JsonResult GetTodayAttendance()
        {
            var data = _hrService.GetTodayAttendance();
            return Json(new { success = true, data = data });
        }

        #endregion

        #region Leave Management

        /// <summary>
        /// Quản lý nghỉ phép
        /// </summary>
        public ActionResult LeaveManagement()
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            var pending = _hrService.GetPendingLeaveRequests();
            ViewBag.PendingRequests = pending;
            ViewBag.Employees = _db.Employees.Where(e => e.IsActive).ToList();
            return View();
        }

        /// <summary>
        /// Tạo đơn xin nghỉ
        /// </summary>
        public ActionResult CreateLeaveRequest()
        {
            ViewBag.LeaveTypes = GetLeaveTypes();
            ViewBag.CurrentBalance = _hrService.GetLeaveBalance(GetCurrentEmployeeId(), DateTime.Now.Year);
            return View(new LeaveRequest { EmployeeId = GetCurrentEmployeeId() });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateLeaveRequest(LeaveRequest request)
        {
            if (ModelState.IsValid)
            {
                var result = _hrService.SubmitLeaveRequest(request);
                if (result.Success)
                {
                    TempData["Success"] = result.Message;
                    return RedirectToAction("LeaveManagement");
                }
                TempData["Error"] = result.Message;
            }

            ViewBag.LeaveTypes = GetLeaveTypes();
            ViewBag.CurrentBalance = _hrService.GetLeaveBalance(request.EmployeeId, DateTime.Now.Year);
            return View(request);
        }

        /// <summary>
        /// Phê duyệt đơn xin nghỉ
        /// </summary>
        [HttpPost]
        public JsonResult ApproveLeaveRequest(int requestId)
        {
            if (!IsAuthorized())
                return Json(new { success = false, message = "Không có quyền!" });

            var result = _hrService.ApproveLeaveRequest(requestId, GetCurrentEmployeeId());
            return Json(result);
        }

        /// <summary>
        /// Từ chối đơn xin nghỉ
        /// </summary>
        [HttpPost]
        public JsonResult RejectLeaveRequest(int requestId, string reason = null)
        {
            if (!IsAuthorized())
                return Json(new { success = false, message = "Không có quyền!" });

            var result = _hrService.RejectLeaveRequest(requestId, GetCurrentEmployeeId(), reason);
            return Json(result);
        }

        /// <summary>
        /// API lấy thống kê nghỉ phép
        /// </summary>
        [HttpGet]
        public JsonResult GetLeaveBalance(int employeeId)
        {
            var balance = _hrService.GetLeaveBalance(employeeId, DateTime.Now.Year);
            return Json(new { success = true, data = balance });
        }

        private List<SelectListItem> GetLeaveTypes()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "Annual", Text = "Nghỉ phép năm" },
                new SelectListItem { Value = "Sick", Text = "Nghỉ ốm" },
                new SelectListItem { Value = "Unpaid", Text = "Nghỉ không lương" },
                new SelectListItem { Value = "Maternity", Text = "Nghỉ thai sản" },
                new SelectListItem { Value = "Personal", Text = "Việc riêng" }
            };
        }

        #endregion

        #region Payroll

        /// <summary>
        /// Quản lý bảng lương
        /// </summary>
        public ActionResult Payroll(int? month = null, int? year = null)
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            var targetMonth = month ?? DateTime.Now.Month;
            var targetYear = year ?? DateTime.Now.Year;

            var viewModel = _hrService.GetPayrollByMonth(targetMonth, targetYear);
            return View(viewModel);
        }

        /// <summary>
        /// Tính lương tháng
        /// </summary>
        [HttpPost]
        public JsonResult CalculatePayroll(int month, int year)
        {
            if (!IsAuthorized())
                return Json(new { success = false, message = "Không có quyền!" });

            var result = _hrService.CalculateMonthlyPayroll(month, year);
            return Json(result);
        }

        /// <summary>
        /// Duyệt bảng lương
        /// </summary>
        [HttpPost]
        public JsonResult ApprovePayroll(int payrollId)
        {
            if (!IsAuthorized())
                return Json(new { success = false, message = "Không có quyền!" });

            var result = _hrService.ApprovePayroll(payrollId);
            return Json(result);
        }

        /// <summary>
        /// Xác nhận thanh toán
        /// </summary>
        [HttpPost]
        public JsonResult MarkPayrollAsPaid(int payrollId)
        {
            if (!IsAuthorized())
                return Json(new { success = false, message = "Không có quyền!" });

            var result = _hrService.MarkPayrollAsPaid(payrollId);
            return Json(result);
        }

        /// <summary>
        /// Chi tiết bảng lương nhân viên
        /// </summary>
        public ActionResult PayrollDetail(int id)
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            var payroll = _db.Payroll
                .Include(p => p.Employee)
                .Include(p => p.Employee.Role)
                .FirstOrDefault(p => p.Id == id);

            if (payroll == null)
            {
                TempData["Error"] = "Không tìm thấy bảng lương!";
                return RedirectToAction("Payroll");
            }

            return View(payroll);
        }

        #endregion

        #region Performance Review

        /// <summary>
        /// Danh sách đánh giá hiệu suất
        /// </summary>
        public ActionResult PerformanceReview(int? employeeId = null, string period = null)
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            var viewModel = _hrService.GetPerformanceReviews(employeeId, period);
            ViewBag.Employees = _db.Employees.Where(e => e.IsActive).ToList();
            return View(viewModel);
        }

        /// <summary>
        /// Tạo đánh giá mới
        /// </summary>
        public ActionResult CreatePerformanceReview()
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            ViewBag.Employees = new SelectList(_db.Employees.Where(e => e.IsActive).ToList(), "Id", "FullName");
            ViewBag.Periods = GetReviewPeriods();
            return View(new PerformanceReview
            {
                ReviewerId = GetCurrentEmployeeId(),
                ReviewDate = DateTime.Today
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreatePerformanceReview(PerformanceReview review)
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            if (ModelState.IsValid)
            {
                var result = _hrService.CreatePerformanceReview(review);
                if (result.Success)
                {
                    TempData["Success"] = result.Message;
                    return RedirectToAction("PerformanceReview");
                }
                TempData["Error"] = result.Message;
            }

            ViewBag.Employees = new SelectList(_db.Employees.Where(e => e.IsActive).ToList(), "Id", "FullName");
            ViewBag.Periods = GetReviewPeriods();
            return View(review);
        }

        private List<SelectListItem> GetReviewPeriods()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "Monthly", Text = "Hàng tháng" },
                new SelectListItem { Value = "Quarterly", Text = "Hàng quý" },
                new SelectListItem { Value = "Annual", Text = "Hàng năm" }
            };
        }

        #endregion

        #region Employee Contract

        /// <summary>
        /// Danh sách hợp đồng
        /// </summary>
        public ActionResult Contracts()
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            var contracts = _hrService.GetAllContracts();
            return View(contracts);
        }

        /// <summary>
        /// Tạo hợp đồng mới
        /// </summary>
        public ActionResult CreateContract()
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            ViewBag.Employees = new SelectList(_db.Employees.Where(e => e.IsActive).ToList(), "Id", "FullName");
            ViewBag.ContractTypes = GetContractTypes();
            return View(new EmployeeContract { StartDate = DateTime.Today });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateContract(EmployeeContract contract)
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            if (ModelState.IsValid)
            {
                var result = _hrService.CreateContract(contract);
                if (result.Success)
                {
                    TempData["Success"] = result.Message;
                    return RedirectToAction("Contracts");
                }
                TempData["Error"] = result.Message;
            }

            ViewBag.Employees = new SelectList(_db.Employees.Where(e => e.IsActive).ToList(), "Id", "FullName");
            ViewBag.ContractTypes = GetContractTypes();
            return View(contract);
        }

        private List<SelectListItem> GetContractTypes()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = "FullTime", Text = "Toàn thời gian" },
                new SelectListItem { Value = "PartTime", Text = "Bán thời gian" },
                new SelectListItem { Value = "Probation", Text = "Thử việc" },
                new SelectListItem { Value = "Freelance", Text = "Tự do" }
            };
        }

        #endregion

        #region Work Schedule

        /// <summary>
        /// Lịch làm việc
        /// </summary>
        public ActionResult Schedule(DateTime? weekStart = null)
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            var start = weekStart ?? GetWeekStart(DateTime.Today);
            var viewModel = _hrService.GetWeeklySchedule(start);
            return View(viewModel);
        }

        /// <summary>
        /// Quản lý ca làm việc
        /// </summary>
        public ActionResult WorkShifts()
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            var shifts = _hrService.GetAllWorkShifts();
            return View(shifts);
        }

        /// <summary>
        /// Tạo ca làm việc
        /// </summary>
        public ActionResult CreateWorkShift()
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            return View(new WorkShift());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateWorkShift(WorkShift shift)
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            if (ModelState.IsValid)
            {
                var result = _hrService.CreateWorkShift(shift);
                if (result.Success)
                {
                    TempData["Success"] = result.Message;
                    return RedirectToAction("WorkShifts");
                }
                TempData["Error"] = result.Message;
            }

            return View(shift);
        }

        /// <summary>
        /// Xếp lịch làm việc
        /// </summary>
        [HttpPost]
        public JsonResult AssignSchedule(int employeeId, int shiftId, DateTime workDate)
        {
            if (!IsAuthorized())
                return Json(new { success = false, message = "Không có quyền!" });

            var result = _hrService.AssignSchedule(employeeId, shiftId, workDate);
            return Json(result);
        }

        private DateTime GetWeekStart(DateTime date)
        {
            int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.AddDays(-diff).Date;
        }

        #endregion

        #region Employee Stats

        /// <summary>
        /// Thống kê chi tiết nhân viên
        /// </summary>
        public ActionResult EmployeeStats(int id)
        {
            if (!IsAuthorized())
                return RedirectUnauthorized();

            var employee = _db.Employees.Include("Role").FirstOrDefault(e => e.Id == id);
            if (employee == null)
            {
                TempData["Error"] = "Không tìm thấy nhân viên!";
                return RedirectToAction("Dashboard");
            }

            var thisMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var attendanceReport = _hrService.GetAttendanceReport(thisMonth, DateTime.Today, id);

            var viewModel = new EmployeeStatsViewModel
            {
                Employee = employee,
                CurrentContract = _hrService.GetActiveContract(id),
                AttendanceStats = attendanceReport.Summary,
                LeaveStats = _hrService.GetLeaveBalance(id, DateTime.Now.Year)
            };

            return View(viewModel);
        }

        #endregion

        #region Database Setup

        /// <summary>
        /// Kiểm tra xem các tables HR đã tồn tại chưa
        /// </summary>
        private bool IsHRTablesExist()
        {
            try
            {
                // Thử query một table để kiểm tra - sử dụng LINQ thay vì raw SQL
                var count = _db.WorkShifts.Count();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Khởi tạo Database HR - tạo các tables và dữ liệu mẫu
        /// </summary>
        [HttpPost]
        public JsonResult SetupHRDatabase()
        {
            if (!IsAuthorized())
                return Json(new { success = false, message = "Không có quyền!" });

            try
            {
                // Tạo các tables
                CreateHRTables();
                
                // Tạo dữ liệu mẫu
                CreateInitialData();

                return Json(new { success = true, message = "Khởi tạo Database HR thành công! Đang tải lại trang..." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi khởi tạo: " + ex.Message });
            }
        }

        /// <summary>
        /// Tạo các tables HR
        /// </summary>
        private void CreateHRTables()
        {
            // WorkShift
            _db.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkShift')
                BEGIN
                    CREATE TABLE [dbo].[WorkShift](
                        [Id] [int] IDENTITY(1,1) NOT NULL,
                        [ShiftName] [nvarchar](50) NOT NULL,
                        [StartTime] [time](7) NOT NULL,
                        [EndTime] [time](7) NOT NULL,
                        [WorkHours] [decimal](5, 2) NULL,
                        [IsActive] [bit] NOT NULL DEFAULT(1),
                        [Description] [nvarchar](200) NULL,
                        CONSTRAINT [PK_WorkShift] PRIMARY KEY CLUSTERED ([Id] ASC)
                    )
                END
            ");

            // Attendance
            _db.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Attendance')
                BEGIN
                    CREATE TABLE [dbo].[Attendance](
                        [Id] [int] IDENTITY(1,1) NOT NULL,
                        [EmployeeId] [int] NOT NULL,
                        [CheckInTime] [datetime] NOT NULL,
                        [CheckOutTime] [datetime] NULL,
                        [WorkHours] [decimal](5, 2) NULL,
                        [Status] [nvarchar](20) NULL,
                        [Note] [nvarchar](500) NULL,
                        [Location] [nvarchar](100) NULL,
                        CONSTRAINT [PK_Attendance] PRIMARY KEY CLUSTERED ([Id] ASC),
                        CONSTRAINT [FK_Attendance_Employee] FOREIGN KEY([EmployeeId]) REFERENCES [dbo].[Employee] ([Id])
                    )
                END
            ");

            // LeaveRequest
            _db.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LeaveRequest')
                BEGIN
                    CREATE TABLE [dbo].[LeaveRequest](
                        [Id] [int] IDENTITY(1,1) NOT NULL,
                        [EmployeeId] [int] NOT NULL,
                        [LeaveType] [nvarchar](50) NOT NULL,
                        [StartDate] [date] NOT NULL,
                        [EndDate] [date] NOT NULL,
                        [Reason] [nvarchar](500) NULL,
                        [Status] [nvarchar](20) NOT NULL DEFAULT('Pending'),
                        [ApprovedBy] [int] NULL,
                        [ApprovedDate] [datetime] NULL,
                        [CreatedDate] [datetime] NOT NULL DEFAULT(GETDATE()),
                        CONSTRAINT [PK_LeaveRequest] PRIMARY KEY CLUSTERED ([Id] ASC),
                        CONSTRAINT [FK_LeaveRequest_Employee] FOREIGN KEY([EmployeeId]) REFERENCES [dbo].[Employee] ([Id])
                    )
                END
            ");

            // EmployeeContract
            _db.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EmployeeContract')
                BEGIN
                    CREATE TABLE [dbo].[EmployeeContract](
                        [Id] [int] IDENTITY(1,1) NOT NULL,
                        [EmployeeId] [int] NOT NULL,
                        [ContractType] [nvarchar](50) NOT NULL,
                        [StartDate] [date] NOT NULL,
                        [EndDate] [date] NULL,
                        [BaseSalary] [decimal](18, 2) NOT NULL,
                        [Allowance] [decimal](18, 2) NULL,
                        [Status] [nvarchar](20) NULL DEFAULT('Active'),
                        [Notes] [nvarchar](500) NULL,
                        [CreatedDate] [datetime] NOT NULL DEFAULT(GETDATE()),
                        CONSTRAINT [PK_EmployeeContract] PRIMARY KEY CLUSTERED ([Id] ASC),
                        CONSTRAINT [FK_EmployeeContract_Employee] FOREIGN KEY([EmployeeId]) REFERENCES [dbo].[Employee] ([Id])
                    )
                END
            ");

            // Payroll
            _db.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Payroll')
                BEGIN
                    CREATE TABLE [dbo].[Payroll](
                        [Id] [int] IDENTITY(1,1) NOT NULL,
                        [EmployeeId] [int] NOT NULL,
                        [Month] [int] NOT NULL,
                        [Year] [int] NOT NULL,
                        [BaseSalary] [decimal](18, 2) NOT NULL,
                        [Allowance] [decimal](18, 2) NOT NULL DEFAULT(0),
                        [OvertimeBonus] [decimal](18, 2) NOT NULL DEFAULT(0),
                        [PerformanceBonus] [decimal](18, 2) NOT NULL DEFAULT(0),
                        [Deduction] [decimal](18, 2) NOT NULL DEFAULT(0),
                        [TotalWorkDays] [decimal](18, 2) NOT NULL DEFAULT(0),
                        [TotalWorkHours] [decimal](18, 2) NOT NULL DEFAULT(0),
                        [LateCount] [int] NOT NULL DEFAULT(0),
                        [AbsentCount] [int] NOT NULL DEFAULT(0),
                        [NetSalary] [decimal](18, 2) NOT NULL,
                        [Status] [nvarchar](20) NULL DEFAULT('Draft'),
                        [PaymentDate] [datetime] NULL,
                        [Notes] [nvarchar](500) NULL,
                        [CreatedDate] [datetime] NOT NULL DEFAULT(GETDATE()),
                        CONSTRAINT [PK_Payroll] PRIMARY KEY CLUSTERED ([Id] ASC),
                        CONSTRAINT [FK_Payroll_Employee] FOREIGN KEY([EmployeeId]) REFERENCES [dbo].[Employee] ([Id])
                    )
                END
            ");

            // PerformanceReview
            _db.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PerformanceReview')
                BEGIN
                    CREATE TABLE [dbo].[PerformanceReview](
                        [Id] [int] IDENTITY(1,1) NOT NULL,
                        [EmployeeId] [int] NOT NULL,
                        [ReviewerId] [int] NOT NULL,
                        [ReviewDate] [date] NOT NULL,
                        [ReviewPeriod] [nvarchar](50) NOT NULL,
                        [ServiceQuality] [int] NOT NULL DEFAULT(3),
                        [Punctuality] [int] NOT NULL DEFAULT(3),
                        [Teamwork] [int] NOT NULL DEFAULT(3),
                        [Communication] [int] NOT NULL DEFAULT(3),
                        [WorkEfficiency] [int] NOT NULL DEFAULT(3),
                        [OverallScore] [decimal](3, 2) NULL,
                        [Strengths] [nvarchar](1000) NULL,
                        [AreasToImprove] [nvarchar](1000) NULL,
                        [Comments] [nvarchar](1000) NULL,
                        [CreatedDate] [datetime] NOT NULL DEFAULT(GETDATE()),
                        CONSTRAINT [PK_PerformanceReview] PRIMARY KEY CLUSTERED ([Id] ASC),
                        CONSTRAINT [FK_PerformanceReview_Employee] FOREIGN KEY([EmployeeId]) REFERENCES [dbo].[Employee] ([Id]),
                        CONSTRAINT [FK_PerformanceReview_Reviewer] FOREIGN KEY([ReviewerId]) REFERENCES [dbo].[Employee] ([Id])
                    )
                END
            ");

            // EmployeeSchedule
            _db.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EmployeeSchedule')
                BEGIN
                    CREATE TABLE [dbo].[EmployeeSchedule](
                        [Id] [int] IDENTITY(1,1) NOT NULL,
                        [EmployeeId] [int] NOT NULL,
                        [WorkShiftId] [int] NOT NULL,
                        [WorkDate] [date] NOT NULL,
                        [Status] [nvarchar](20) NULL DEFAULT('Scheduled'),
                        [Note] [nvarchar](200) NULL,
                        CONSTRAINT [PK_EmployeeSchedule] PRIMARY KEY CLUSTERED ([Id] ASC),
                        CONSTRAINT [FK_EmployeeSchedule_Employee] FOREIGN KEY([EmployeeId]) REFERENCES [dbo].[Employee] ([Id]),
                        CONSTRAINT [FK_EmployeeSchedule_WorkShift] FOREIGN KEY([WorkShiftId]) REFERENCES [dbo].[WorkShift] ([Id])
                    )
                END
            ");
        }

        /// <summary>
        /// Tạo dữ liệu khởi tạo
        /// </summary>
        private void CreateInitialData()
        {
            var employees = _db.Employees.Where(e => e.IsActive).ToList();
            if (!employees.Any()) return;

            // Tạo ca làm việc mẫu
            var shiftCount = _db.WorkShifts.Count();
            if (shiftCount == 0)
            {
                _db.Database.ExecuteSqlRaw(@"
                    INSERT INTO [dbo].[WorkShift] ([ShiftName], [StartTime], [EndTime], [WorkHours], [IsActive], [Description])
                    VALUES 
                        (N'Ca sáng', '06:00:00', '14:00:00', 8.0, 1, N'Ca làm việc buổi sáng từ 6h-14h'),
                        (N'Ca chiều', '14:00:00', '22:00:00', 8.0, 1, N'Ca làm việc buổi chiều từ 14h-22h'),
                        (N'Ca tối', '17:00:00', '23:00:00', 6.0, 1, N'Ca làm việc buổi tối từ 17h-23h'),
                        (N'Ca full-day', '08:00:00', '17:00:00', 8.0, 1, N'Ca làm việc cả ngày 8h-17h')
                ");
            }

            // Tạo hợp đồng mẫu cho nhân viên
            foreach (var emp in employees)
            {
                var hasContract = _db.EmployeeContracts.Any(c => c.EmployeeId == emp.Id && c.Status == "Active");
                
                if (!hasContract)
                {
                    var salary = GetSalaryByRole(emp.RoleId);
                    _db.Database.ExecuteSqlRaw(@"
                        INSERT INTO EmployeeContract (EmployeeId, ContractType, StartDate, BaseSalary, Allowance, Status, CreatedDate)
                        VALUES ({0}, 'FullTime', {1}, {2}, 500000, 'Active', GETDATE())
                    ", emp.Id, emp.HireDate, salary);
                }
            }

            // Tạo chấm công mẫu 7 ngày gần nhất
            var random = new Random();
            for (int day = 6; day >= 0; day--)
            {
                var date = DateTime.Today.AddDays(-day);
                if (date.DayOfWeek == DayOfWeek.Sunday) continue;

                foreach (var emp in employees.Take(5))
                {
                    var hasAttendance = _db.Attendances.Any(a => a.EmployeeId == emp.Id && a.CheckInTime.Date == date.Date);

                    if (!hasAttendance)
                    {
                        var checkInHour = 7 + random.Next(0, 2);
                        var checkInMinute = random.Next(0, 60);
                        var checkIn = date.AddHours(checkInHour).AddMinutes(checkInMinute);
                        var checkOut = date.AddHours(17).AddMinutes(random.Next(-30, 60));
                        var workHours = (decimal)(checkOut - checkIn).TotalHours;
                        var status = checkIn.TimeOfDay <= new TimeSpan(8, 15, 0) ? "OnTime" : "Late";

                        _db.Database.ExecuteSqlRaw(@"
                            INSERT INTO Attendance (EmployeeId, CheckInTime, CheckOutTime, WorkHours, Status, Note)
                            VALUES ({0}, {1}, {2}, {3}, {4}, N'Chấm công tự động')
                        ", emp.Id, checkIn, checkOut, workHours, status);
                    }
                }
            }

            // Tạo đánh giá hiệu suất mẫu
            var reviewerId = employees.FirstOrDefault(e => e.RoleId == 1 || e.RoleId == 2)?.Id ?? employees.First().Id;
            foreach (var emp in employees.Take(3).Where(e => e.Id != reviewerId))
            {
                var hasReview = _db.PerformanceReviews.Any(r => r.EmployeeId == emp.Id);

                if (!hasReview)
                {
                    var score1 = 3 + random.Next(0, 3);
                    var score2 = 3 + random.Next(0, 3);
                    var score3 = 3 + random.Next(0, 3);
                    var score4 = 3 + random.Next(0, 3);
                    var score5 = 3 + random.Next(0, 3);
                    var overall = (score1 + score2 + score3 + score4 + score5) / 5.0m;

                    _db.Database.ExecuteSqlRaw(@"
                        INSERT INTO PerformanceReview 
                        (EmployeeId, ReviewerId, ReviewDate, ReviewPeriod, ServiceQuality, Punctuality, Teamwork, Communication, WorkEfficiency, OverallScore, Strengths, AreasToImprove, Comments, CreatedDate)
                        VALUES ({0}, {1}, {2}, 'Monthly', {3}, {4}, {5}, {6}, {7}, {8}, N'Làm việc chăm chỉ, có tinh thần trách nhiệm', N'Cần cải thiện kỹ năng giao tiếp', N'Nhân viên có tiềm năng phát triển tốt', GETDATE())
                    ", emp.Id, reviewerId, DateTime.Today.AddDays(-random.Next(1, 30)), score1, score2, score3, score4, score5, overall);
                }
            }
        }

        #endregion

        #region Sample Data

        /// <summary>
        /// Tạo dữ liệu mẫu HR
        /// </summary>
        [HttpPost]
        public JsonResult CreateSampleData()
        {
            if (!IsAuthorized())
                return Json(new { success = false, message = "Không có quyền!" });

            try
            {
                var employees = _db.Employees.Where(e => e.IsActive).ToList();

                // Tạo ca làm việc mẫu
                if (!_db.Set<WorkShift>().Any())
                {
                    _db.Set<WorkShift>().Add(new WorkShift
                    {
                        ShiftName = "Ca sáng",
                        StartTime = new TimeSpan(6, 0, 0),
                        EndTime = new TimeSpan(14, 0, 0),
                        WorkHours = 8,
                        IsActive = true,
                        Description = "Ca làm việc buổi sáng"
                    });
                    _db.Set<WorkShift>().Add(new WorkShift
                    {
                        ShiftName = "Ca chiều",
                        StartTime = new TimeSpan(14, 0, 0),
                        EndTime = new TimeSpan(22, 0, 0),
                        WorkHours = 8,
                        IsActive = true,
                        Description = "Ca làm việc buổi chiều"
                    });
                    _db.Set<WorkShift>().Add(new WorkShift
                    {
                        ShiftName = "Ca tối",
                        StartTime = new TimeSpan(17, 0, 0),
                        EndTime = new TimeSpan(23, 0, 0),
                        WorkHours = 6,
                        IsActive = true,
                        Description = "Ca làm việc buổi tối"
                    });
                }

                // Tạo hợp đồng mẫu cho nhân viên chưa có
                foreach (var emp in employees)
                {
                    if (!_db.Set<EmployeeContract>().Any(c => c.EmployeeId == emp.Id && c.Status == "Active"))
                    {
                        _db.Set<EmployeeContract>().Add(new EmployeeContract
                        {
                            EmployeeId = emp.Id,
                            ContractType = "FullTime",
                            StartDate = emp.HireDate,
                            BaseSalary = GetSalaryByRole(emp.RoleId),
                            Allowance = 500000,
                            Status = "Active",
                            CreatedDate = DateTime.Now
                        });
                    }
                }

                // Tạo chấm công mẫu 7 ngày gần nhất
                var random = new Random();
                for (int day = 6; day >= 0; day--)
                {
                    var date = DateTime.Today.AddDays(-day);
                    if (date.DayOfWeek == DayOfWeek.Sunday) continue;

                    foreach (var emp in employees.Take(5))
                    {
                        if (!_db.Set<Attendance>().Any(a => a.EmployeeId == emp.Id &&
                            System.Data.Entity.a.CheckInTime.Date == date))
                        {
                            var checkIn = date.AddHours(7).AddMinutes(random.Next(0, 60));
                            var checkOut = date.AddHours(17).AddMinutes(random.Next(-30, 60));
                            var status = checkIn.TimeOfDay <= new TimeSpan(8, 15, 0) ? "OnTime" : "Late";

                            _db.Set<Attendance>().Add(new Attendance
                            {
                                EmployeeId = emp.Id,
                                CheckInTime = checkIn,
                                CheckOutTime = checkOut,
                                WorkHours = (decimal)(checkOut - checkIn).TotalHours,
                                Status = status
                            });
                        }
                    }
                }

                _db.SaveChanges();

                return Json(new { success = true, message = "Đã tạo dữ liệu mẫu HR thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        private decimal GetSalaryByRole(int roleId)
        {
            switch (roleId)
            {
                case 1: return 15000000; // Admin
                case 2: return 12000000; // Manager
                case 3: return 8000000;  // Cashier
                case 4: return 7000000;  // Waiter
                case 5: return 9000000;  // Chef
                default: return 6000000;
            }
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
