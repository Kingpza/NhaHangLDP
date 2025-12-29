using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NhaHangLDP.Models;

namespace NhaHangLDP.Services.Management
{
    public class EmployeeAdminService
    {
        private readonly MyDbContext db;

        public EmployeeAdminService(MyDbContext context)
        {
            db = context;
        }

        public List<Employee> GetAllEmployees()
        {
            return db.Employees.Include("Role").ToList();
        }

        public Employee GetEmployeeById(int id)
        {
            return db.Employees.Find(id);
        }

        public List<Role> GetAllRoles()
        {
            return db.Roles.ToList();
        }

        public bool IsUsernameExists(string username, int? excludeId = null)
        {
            if (excludeId.HasValue)
            {
                return db.Employees.Any(e => e.UserName == username && e.Id != excludeId.Value);
            }
            return db.Employees.Any(e => e.UserName == username);
        }

        public bool CreateEmployee(Employee employee, out string errorMessage)
        {
            try
            {
                employee.PasswordHash = HashPassword(employee.PasswordHash);
                db.Employees.Add(employee);
                db.SaveChanges();

                errorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Có lỗi xảy ra khi lưu: " + ex.Message;
                return false;
            }
        }

        public bool UpdateEmployee(Employee employee, string newPassword, out string errorMessage)
        {
            try
            {
                var empInDb = db.Employees.Find(employee.Id);
                if (empInDb == null)
                {
                    errorMessage = "Không tìm thấy nhân viên.";
                    return false;
                }

                empInDb.FullName = employee.FullName;
                empInDb.Email = employee.Email;
                empInDb.PhoneNumber = employee.PhoneNumber;
                empInDb.RoleId = employee.RoleId;
                empInDb.UserName = employee.UserName;
                empInDb.IsActive = employee.IsActive;

                if (!string.IsNullOrEmpty(newPassword))
                {
                    empInDb.PasswordHash = HashPassword(newPassword);
                }

                db.SaveChanges();

                errorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Có lỗi xảy ra khi cập nhật: " + ex.Message;
                return false;
            }
        }

        public bool ToggleEmployeeStatus(int id, string currentUserName, out string errorMessage, out bool newStatus)
        {
            try
            {
                var employee = db.Employees.Find(id);
                if (employee == null)
                {
                    errorMessage = "Không tìm thấy nhân viên.";
                    newStatus = false;
                    return false;
                }

                if (employee.UserName == currentUserName)
                {
                    errorMessage = "Bạn không thể tự khóa tài khoản của chính mình.";
                    newStatus = employee.IsActive;
                    return false;
                }

                employee.IsActive = !employee.IsActive;
                db.SaveChanges();

                newStatus = employee.IsActive;
                errorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Có lỗi xảy ra: " + ex.Message;
                newStatus = false;
                return false;
            }
        }

        public string HashPassword(string password)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        public bool CreateSampleEmployees(out string errorMessage, out int employeeCount, out int roleCount)
        {
            try
            {
                if (db.Employees.Any())
                {
                    errorMessage = "Đã có nhân viên trong hệ thống!";
                    employeeCount = 0;
                    roleCount = 0;
                    return false;
                }

                var roles = new List<Role>();
                if (!db.Roles.Any(r => r.RoleName == "Admin")) roles.Add(new Role { RoleName = "Admin" });
                if (!db.Roles.Any(r => r.RoleName == "Manager")) roles.Add(new Role { RoleName = "Manager" });
                if (!db.Roles.Any(r => r.RoleName == "Cashier")) roles.Add(new Role { RoleName = "Cashier" });
                if (!db.Roles.Any(r => r.RoleName == "Staff")) roles.Add(new Role { RoleName = "Staff" });
                if (!db.Roles.Any(r => r.RoleName == "Kitchen")) roles.Add(new Role { RoleName = "Kitchen" });

                foreach (var role in roles)
                {
                    db.Roles.Add(role);
                }
                db.SaveChanges();

                var adminRole = db.Roles.FirstOrDefault(r => r.RoleName == "Admin");
                var managerRole = db.Roles.FirstOrDefault(r => r.RoleName == "Manager");
                var cashierRole = db.Roles.FirstOrDefault(r => r.RoleName == "Cashier");
                var staffRole = db.Roles.FirstOrDefault(r => r.RoleName == "Staff");
                var kitchenRole = db.Roles.FirstOrDefault(r => r.RoleName == "Kitchen");

                var sampleEmployees = new List<Employee>
                {
                    new Employee { FullName = "Nguyễn Văn Admin", UserName = "admin", PasswordHash = HashPassword("admin123"), Email = "admin@nhahang.com", PhoneNumber = "0901234567", RoleId = adminRole?.Id ?? 1, HireDate = DateTime.Now.AddMonths(-12), IsActive = true },
                    new Employee { FullName = "Trần Thị Manager", UserName = "manager", PasswordHash = HashPassword("manager123"), Email = "manager@nhahang.com", PhoneNumber = "0902345678", RoleId = managerRole?.Id ?? 2, HireDate = DateTime.Now.AddMonths(-10), IsActive = true },
                    new Employee { FullName = "Lê Văn Thu Ngân", UserName = "cashier1", PasswordHash = HashPassword("cashier123"), Email = "cashier1@nhahang.com", PhoneNumber = "0903456789", RoleId = cashierRole?.Id ?? 3, HireDate = DateTime.Now.AddMonths(-8), IsActive = true },
                    new Employee { FullName = "Hoàng Thị Thu Ngân 2", UserName = "cashier2", PasswordHash = HashPassword("cashier123"), Email = "cashier2@nhahang.com", PhoneNumber = "0904567890", RoleId = cashierRole?.Id ?? 3, HireDate = DateTime.Now.AddMonths(-6), IsActive = true },
                    new Employee { FullName = "Phạm Văn Bếp Trưởng", UserName = "chef", PasswordHash = HashPassword("chef123"), Email = "chef@nhahang.com", PhoneNumber = "0905678901", RoleId = kitchenRole?.Id ?? 5, HireDate = DateTime.Now.AddMonths(-9), IsActive = true },
                    new Employee { FullName = "Vũ Thị Phụ Bếp", UserName = "kitchen1", PasswordHash = HashPassword("kitchen123"), Email = "kitchen1@nhahang.com", PhoneNumber = "0906789012", RoleId = kitchenRole?.Id ?? 5, HireDate = DateTime.Now.AddMonths(-4), IsActive = true },
                    new Employee { FullName = "Đỗ Văn Nhân Viên", UserName = "staff1", PasswordHash = HashPassword("staff123"), Email = "staff1@nhahang.com", PhoneNumber = "0907890123", RoleId = staffRole?.Id ?? 4, HireDate = DateTime.Now.AddMonths(-3), IsActive = true },
                    new Employee { FullName = "Bùi Thị Nhân Viên 2", UserName = "staff2", PasswordHash = HashPassword("staff123"), Email = "staff2@nhahang.com", PhoneNumber = "0908901234", RoleId = staffRole?.Id ?? 4, HireDate = DateTime.Now.AddMonths(-2), IsActive = true },
                    new Employee { FullName = "Lý Văn Phục Vụ", UserName = "waiter1", PasswordHash = HashPassword("waiter123"), Email = "waiter1@nhahang.com", PhoneNumber = "0909012345", RoleId = staffRole?.Id ?? 4, HireDate = DateTime.Now.AddMonths(-1), IsActive = true },
                    new Employee { FullName = "Mai Thị Phục Vụ 2", UserName = "waiter2", PasswordHash = HashPassword("waiter123"), Email = "waiter2@nhahang.com", PhoneNumber = "0900123456", RoleId = staffRole?.Id ?? 4, HireDate = DateTime.Now.AddDays(-15), IsActive = false }
                };

                foreach (var employee in sampleEmployees)
                {
                    db.Employees.Add(employee);
                }

                db.SaveChanges();

                errorMessage = null;
                employeeCount = sampleEmployees.Count;
                roleCount = roles.Count;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Có lỗi xảy ra khi tạo nhân viên mẫu: " + ex.Message;
                employeeCount = 0;
                roleCount = 0;
                return false;
            }
        }
    }
}
