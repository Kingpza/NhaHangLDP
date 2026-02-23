using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using NhaHangLDP.Models;
using NhaHangLDP.Filters;
using System.Security.Cryptography;
using System.Text;

namespace NhaHangLDP.Controllers
{
    /// <summary>
    /// Controller quản lý tài khoản hệ thống (Account Management)
    /// </summary>
    [CustomAuthorize("Admin", "Manager")]
    public class AccountManagementController : Controller
    {
        private readonly NhaHangLDPEntities _db = new NhaHangLDPEntities();

        /// <summary>
        /// Trang danh sách tài khoản
        /// </summary>
        public ActionResult Index()
        {
            var accounts = _db.Account
                .Select(a => new AccountManagementViewModel
                {
                    Id = a.Id,
                    Username = a.Username,
                    FullName = a.FullName,
                    Email = a.Email,
                    Phone = a.PhoneNumber,
                    RoleId = a.RoleId,
                    RoleName = a.Role.RoleName,
                    IsActive = a.IsActive,
                    CreatedDate = a.CreatedDate,
                    LastLogin = a.LastLoginDate
                }).ToList();

            var roles = _db.Role.ToList();
            ViewBag.Roles = roles;

            // Load Permission Data for Permissions Tab
            var permissionModel = GetPermissionManagementViewModel();
            ViewBag.PermissionModel = permissionModel;

            return View(accounts);
        }

        /// <summary>
        /// Helper method to get Permission Management ViewModel
        /// </summary>
        private NhaHangLDP.Models.PermissionManagementViewModel GetPermissionManagementViewModel()
        {
            var roles = _db.Role.ToList();
            var availablePages = GetAvailablePages();

            var rolePermissions = new List<NhaHangLDP.Models.RolePermissionViewModel>();

            foreach (var role in roles)
            {
                var permissions = new List<NhaHangLDP.Models.PermissionItem>();

                try
                {
                    permissions = _db.Database.SqlQuery<NhaHangLDP.Models.PermissionItem>(
                        @"SELECT Id, RoleId, Controller, ActionName, Description, CanView, CanAdd, CanEdit, CanDelete
                          FROM RolePermission WHERE RoleId = @p0", role.Id).ToList();
                }
                catch
                {
                    // Table may not exist yet
                }

                var accountCount = _db.Account.Count(a => a.RoleId == role.Id);

                rolePermissions.Add(new NhaHangLDP.Models.RolePermissionViewModel
                {
                    RoleId = role.Id,
                    RoleName = role.RoleName,
                    AccountCount = accountCount,
                    Permissions = permissions
                });
            }

            return new NhaHangLDP.Models.PermissionManagementViewModel
            {
                Roles = rolePermissions,
                AvailablePages = availablePages
            };
        }

        /// <summary>
        /// Get available pages for permission management
        /// </summary>
        private List<NhaHangLDP.Models.PagePermissionItem> GetAvailablePages()
        {
            return new List<NhaHangLDP.Models.PagePermissionItem>
            {
                new NhaHangLDP.Models.PagePermissionItem { Controller = "Management", ActionName = "Menu", DisplayName = "Quản lý thực đơn", Icon = "utensils", Category = "Quản lý" },
                new NhaHangLDP.Models.PagePermissionItem { Controller = "Management", ActionName = "Tables", DisplayName = "Quản lý bàn", Icon = "chair", Category = "Quản lý" },
                new NhaHangLDP.Models.PagePermissionItem { Controller = "Kitchen", ActionName = "Index", DisplayName = "Quản lý bếp", Icon = "fire-burner", Category = "Vận hành" },
                new NhaHangLDP.Models.PagePermissionItem { Controller = "Delivery", ActionName = "Index", DisplayName = "Quản lý giao hàng", Icon = "motorcycle", Category = "Vận hành" },
                new NhaHangLDP.Models.PagePermissionItem { Controller = "Payment", ActionName = "Index", DisplayName = "Quản lý thanh toán", Icon = "credit-card", Category = "Tài chính" },
                new NhaHangLDP.Models.PagePermissionItem { Controller = "HR", ActionName = "Index", DisplayName = "Quản lý nhân sự", Icon = "users-cog", Category = "Nhân sự" },
                new NhaHangLDP.Models.PagePermissionItem { Controller = "ReportsManagement", ActionName = "Index", DisplayName = "Báo cáo & thống kê", Icon = "chart-line", Category = "Tài chính" },
                new NhaHangLDP.Models.PagePermissionItem { Controller = "Settings", ActionName = "Index", DisplayName = "Cài đặt hệ thống", Icon = "cog", Category = "Hệ thống" },
                new NhaHangLDP.Models.PagePermissionItem { Controller = "Promotion", ActionName = "Index", DisplayName = "Quản lý khuyến mãi", Icon = "tags", Category = "Marketing" },
                new NhaHangLDP.Models.PagePermissionItem { Controller = "QRCode", ActionName = "Index", DisplayName = "Quản lý QR Code", Icon = "qrcode", Category = "Vận hành" },
                new NhaHangLDP.Models.PagePermissionItem { Controller = "Email", ActionName = "Index", DisplayName = "Quản lý Email", Icon = "envelope", Category = "Marketing" },
                new NhaHangLDP.Models.PagePermissionItem { Controller = "AccountManagement", ActionName = "Index", DisplayName = "Quản lý tài khoản & phân quyền", Icon = "users-cog", Category = "Hệ thống" }
            };
        }

        /// <summary>
        /// Tạo tài khoản mới
        /// </summary>
        [HttpGet]
        public ActionResult Create()
        {
            ViewBag.Roles = _db.Role.ToList();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(CreateAccountDto model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    ViewBag.Roles = _db.Role.ToList();
                    return View(model);
                }

                // Check if username exists
                if (_db.Account.Any(a => a.Username == model.Username))
                {
                    TempData["Error"] = "Tên đăng nhập đã tồn tại!";
                    ViewBag.Roles = _db.Role.ToList();
                    return View(model);
                }

                // Check if email exists
                if (!string.IsNullOrEmpty(model.Email) && _db.Account.Any(a => a.Email == model.Email))
                {
                    TempData["Error"] = "Email đã được sử dụng!";
                    ViewBag.Roles = _db.Role.ToList();
                    return View(model);
                }

                var account = new Account
                {
                    Username = model.Username,
                    PasswordHash = HashPassword(model.Password),
                    FullName = model.FullName,
                    Email = model.Email,
                    PhoneNumber = model.Phone,
                    RoleId = model.RoleId,
                    IsActive = true,
                    CreatedDate = DateTime.Now
                };

                _db.Account.Add(account);
                _db.SaveChanges();

                TempData["Success"] = "Tạo tài khoản thành công!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi: " + ex.Message;
                ViewBag.Roles = _db.Role.ToList();
                return View(model);
            }
        }

        /// <summary>
        /// Cập nhật vai trò cho tài khoản
        /// </summary>
        [HttpPost]
        public JsonResult UpdateRole(int accountId, int roleId)
        {
            try
            {
                var account = _db.Account.Find(accountId);
                if (account == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy tài khoản!" });
                }

                account.RoleId = roleId;
                _db.SaveChanges();

                var roleName = _db.Role.Find(roleId)?.RoleName;
                return Json(new { success = true, message = "Cập nhật vai trò thành công!", roleName = roleName });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// Khóa/Mở khóa tài khoản
        /// </summary>
        [HttpPost]
        public JsonResult ToggleStatus(int accountId)
        {
            try
            {
                var account = _db.Account.Find(accountId);
                if (account == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy tài khoản!" });
                }

                account.IsActive = !account.IsActive;
                _db.SaveChanges();

                var status = account.IsActive ? "mở khóa" : "khóa";
                return Json(new { success = true, message = $"Đã {status} tài khoản!", isActive = account.IsActive });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// Đặt lại mật khẩu
        /// </summary>
        [HttpPost]
        public JsonResult ResetPassword(int accountId, string newPassword)
        {
            try
            {
                if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 6)
                {
                    return Json(new { success = false, message = "Mật khẩu phải có ít nhất 6 ký tự!" });
                }

                var account = _db.Account.Find(accountId);
                if (account == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy tài khoản!" });
                }

                account.PasswordHash = HashPassword(newPassword);
                _db.SaveChanges();

                return Json(new { success = true, message = "Đặt lại mật khẩu thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// Xóa tài khoản
        /// </summary>
        [HttpPost]
        public JsonResult Delete(int accountId)
        {
            try
            {
                var account = _db.Account.Find(accountId);
                if (account == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy tài khoản!" });
                }

                // Don't allow deleting own account
                var currentUserId = Session["UserId"] as int?;
                if (currentUserId == accountId)
                {
                    return Json(new { success = false, message = "Không thể xóa tài khoản của chính mình!" });
                }

                _db.Account.Remove(account);
                _db.SaveChanges();

                return Json(new { success = true, message = "Xóa tài khoản thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// Lấy thông tin chi tiết tài khoản
        /// </summary>
        [HttpGet]
        public JsonResult GetAccountDetails(int accountId)
        {
            try
            {
                var account = _db.Account
                    .Where(a => a.Id == accountId)
                    .Select(a => new
                    {
                        a.Id,
                        a.Username,
                        a.FullName,
                        a.Email,
                        Phone = a.PhoneNumber,
                        a.RoleId,
                        RoleName = a.Role.RoleName,
                        a.IsActive,
                        a.CreatedDate,
                        LastLogin = a.LastLoginDate
                    })
                    .FirstOrDefault();

                if (account == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy tài khoản!" }, JsonRequestBehavior.AllowGet);
                }

                return Json(new { success = true, data = account }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        private string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                foreach (byte b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _db.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Permission Management Methods

        /// <summary>
        /// Cập nhật quyền cho 1 role
        /// </summary>
        [HttpPost]
        public JsonResult UpdatePermission(UpdatePermissionDto dto)
        {
            try
            {
                if (dto == null || dto.RoleId <= 0)
                {
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ!" });
                }

                var existingCount = _db.Database.SqlQuery<int>(
                    "SELECT COUNT(*) FROM RolePermission WHERE RoleId = @p0 AND Controller = @p1 AND ActionName = @p2",
                    dto.RoleId, dto.Controller, dto.ActionName).FirstOrDefault();

                if (existingCount > 0)
                {
                    _db.Database.ExecuteSqlCommand(
                        @"UPDATE RolePermission 
                          SET CanView = @p0, CanAdd = @p1, CanEdit = @p2, CanDelete = @p3, 
                              Description = @p4, UpdatedDate = GETDATE()
                          WHERE RoleId = @p5 AND Controller = @p6 AND ActionName = @p7",
                        dto.CanView, dto.CanAdd, dto.CanEdit, dto.CanDelete,
                        dto.Description, dto.RoleId, dto.Controller, dto.ActionName);
                }
                else
                {
                    _db.Database.ExecuteSqlCommand(
                        @"INSERT INTO RolePermission (RoleId, Controller, ActionName, CanView, CanAdd, CanEdit, CanDelete, Description, CreatedDate)
                          VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, GETDATE())",
                        dto.RoleId, dto.Controller, dto.ActionName,
                        dto.CanView, dto.CanAdd, dto.CanEdit, dto.CanDelete, dto.Description);
                }

                return Json(new { success = true, message = "Cập nhật quyền thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// Xóa quyền
        /// </summary>
        [HttpPost]
        public JsonResult DeletePermission(int roleId, string controller, string actionName)
        {
            try
            {
                _db.Database.ExecuteSqlCommand(
                    "DELETE FROM RolePermission WHERE RoleId = @p0 AND Controller = @p1 AND ActionName = @p2",
                    roleId, controller, actionName);

                return Json(new { success = true, message = "Đã xóa quyền!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// Lấy quyền của 1 role (JSON)
        /// </summary>
        [HttpGet]
        public JsonResult GetRolePermissions(int roleId)
        {
            try
            {
                var permissions = _db.Database.SqlQuery<NhaHangLDP.Models.PermissionItem>(
                    @"SELECT Id, RoleId, Controller, ActionName, Description, CanView, CanAdd, CanEdit, CanDelete
                      FROM RolePermission WHERE RoleId = @p0", roleId).ToList();

                return Json(new { success = true, data = permissions }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// Cập nhật hàng loạt quyền cho 1 role
        /// </summary>
        [HttpPost]
        public JsonResult BulkUpdatePermissions(int roleId, List<UpdatePermissionDto> permissions)
        {
            try
            {
                if (permissions == null || !permissions.Any())
                {
                    return Json(new { success = false, message = "Không có dữ liệu quyền!" });
                }

                _db.Database.ExecuteSqlCommand(
                    "DELETE FROM RolePermission WHERE RoleId = @p0", roleId);

                foreach (var perm in permissions)
                {
                    if (perm.CanView || perm.CanAdd || perm.CanEdit || perm.CanDelete)
                    {
                        _db.Database.ExecuteSqlCommand(
                            @"INSERT INTO RolePermission (RoleId, Controller, ActionName, CanView, CanAdd, CanEdit, CanDelete, Description, CreatedDate)
                              VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, GETDATE())",
                            roleId, perm.Controller, perm.ActionName,
                            perm.CanView, perm.CanAdd, perm.CanEdit, perm.CanDelete, perm.Description);
                    }
                }

                return Json(new { success = true, message = "Cập nhật quyền thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        #endregion
    }

    #region DTOs and ViewModels

    public class AccountManagementViewModel
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public int RoleId { get; set; }
        public string RoleName { get; set; }
        public bool IsActive { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastLogin { get; set; }
    }

    public class CreateAccountDto
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public int RoleId { get; set; }
    }

    public class UpdatePermissionDto
    {
        public int RoleId { get; set; }
        public string Controller { get; set; }
        public string ActionName { get; set; }
        public bool CanView { get; set; }
        public bool CanAdd { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
        public string Description { get; set; }
    }

    #endregion
}
