using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using NhaHangLDP.Data.Entities;
using System.Linq;
using System.Threading.Tasks;
using NhaHangLDP.Models;

namespace NhaHangLDP.Services
{
    public class EmployeeManagementService
    {
        private readonly NhaHangLDPEntities db;

        public EmployeeManagementService(NhaHangLDPEntities context)
        {
            db = context;
        }

        public List<object> GetAvailableEmployees(int currentEmployeeId)
        {
            var availableRoleIds = new List<int> { 1, 2, 3, 4, 5 };

            var employees = db.Employee
                .Where(e => e.IsActive
                            && availableRoleIds.Contains(e.RoleId)
                            && e.Id != currentEmployeeId)
                .Select(e => new
                {
                    id = e.Id,
                    fullName = e.FullName
                })
                .ToList<object>();

            return employees;
        }

        public async Task<OperationResult> RemoveSupportEmployee(int shiftId, int employeeId)
        {
            try
            {
                var supportStaffEntry = await db.ShiftSupportStaff
                    .FirstOrDefaultAsync(s => s.CashierShiftId == shiftId && s.EmployeeId == employeeId);

                if (supportStaffEntry == null)
                {
                    return new OperationResult { Success = false, ErrorMessage = "Không tìm thấy nhân viên hỗ trợ này trong ca." };
                }

                db.ShiftSupportStaff.Remove(supportStaffEntry);
                await db.SaveChangesAsync();

                return new OperationResult { Success = true };
            }
            catch (Exception ex)
            {
                return new OperationResult { Success = false, ErrorMessage = "Lỗi hệ thống: " + ex.Message };
            }
        }

        public async Task<OperationResult> AddSupportEmployee(int shiftId, int employeeId)
        {
            try
            {
                var activeShift = await db.CashierShift
                    .Include(s => s.ShiftSupportStaffs)
                    .FirstOrDefaultAsync(s => s.Id == shiftId && s.EndTime == null);

                if (activeShift == null)
                {
                    return new OperationResult { Success = false, ErrorMessage = "Không tìm thấy ca làm việc." };
                }

                bool isAlreadyInShift = activeShift.CashierId == employeeId ||
                                        activeShift.ShiftSupportStaffs.Any(s => s.EmployeeId == employeeId);

                if (isAlreadyInShift)
                {
                    return new OperationResult { Success = false, ErrorMessage = "Nhân viên đã có trong ca." };
                }

                var supportStaff = new ShiftSupportStaff
                {
                    CashierShiftId = shiftId,
                    EmployeeId = employeeId
                };

                db.ShiftSupportStaff.Add(supportStaff);
                await db.SaveChangesAsync();

                return new OperationResult { Success = true };
            }
            catch (Exception ex)
            {
                return new OperationResult { Success = false, ErrorMessage = "Đã xảy ra lỗi hệ thống: " + ex.Message };
            }
        }

        public async Task<OperationResult> HandoverShift(int shiftId, int newEmployeeId)
        {
            try
            {
                var shift = await db.CashierShift
                    .Include(s => s.ShiftSupportStaffs)
                    .FirstOrDefaultAsync(s => s.Id == shiftId && s.EndTime == null);

                if (shift == null)
                {
                    return new OperationResult { Success = false, ErrorMessage = "Không tìm thấy ca làm việc." };
                }

                if (shift.ShiftSupportStaffs != null && shift.ShiftSupportStaffs.Any())
                {
                    db.ShiftSupportStaff.RemoveRange(shift.ShiftSupportStaffs);
                }

                shift.CashierId = newEmployeeId;
                await db.SaveChangesAsync();

                return new OperationResult { Success = true };
            }
            catch (Exception ex)
            {
                return new OperationResult { Success = false, ErrorMessage = "Lỗi hệ thống khi giao ca: " + ex.Message };
            }
        }

        public class OperationResult
        {
            public bool Success { get; set; }
            public string ErrorMessage { get; set; }
        }
    }
}
