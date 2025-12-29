using System;
using System.Collections.Generic;

namespace NhaHangLDP.Models;

public partial class Employee
{
    public int Id { get; set; }

    public int RoleId { get; set; }

    public string FullName { get; set; }

    public string UserName { get; set; }

    public string PasswordHash { get; set; }

    public string PhoneNumber { get; set; }

    public string Email { get; set; }

    public DateTime HireDate { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();

    public virtual ICollection<Bill> Bills { get; set; } = new List<Bill>();

    public virtual ICollection<CashierShift> CashierShifts { get; set; } = new List<CashierShift>();

    public virtual ICollection<DamagedStock> DamagedStocks { get; set; } = new List<DamagedStock>();

    public virtual ICollection<EmployeeContract> EmployeeContracts { get; set; } = new List<EmployeeContract>();

    public virtual ICollection<EmployeeSchedule> EmployeeSchedules { get; set; } = new List<EmployeeSchedule>();

    public virtual ICollection<KitchenOrderTicket> KitchenOrderTickets { get; set; } = new List<KitchenOrderTicket>();

    public virtual ICollection<LeaveRequest> LeaveRequestApprovedByNavigations { get; set; } = new List<LeaveRequest>();

    public virtual ICollection<LeaveRequest> LeaveRequestEmployees { get; set; } = new List<LeaveRequest>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<Payroll> Payrolls { get; set; } = new List<Payroll>();

    public virtual ICollection<PerformanceReview> PerformanceReviewEmployees { get; set; } = new List<PerformanceReview>();

    public virtual ICollection<PerformanceReview> PerformanceReviewReviewers { get; set; } = new List<PerformanceReview>();

    public virtual ICollection<PriceHistory> PriceHistories { get; set; } = new List<PriceHistory>();

    public virtual ICollection<QROrder> QROrders { get; set; } = new List<QROrder>();

    public virtual ICollection<ReturnBill> ReturnBills { get; set; } = new List<ReturnBill>();

    public virtual Role Role { get; set; }

    public virtual ICollection<ShiftSupportStaff> ShiftSupportStaffs { get; set; } = new List<ShiftSupportStaff>();

    public virtual ICollection<StockInbound> StockInbounds { get; set; } = new List<StockInbound>();
}
