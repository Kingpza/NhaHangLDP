using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using NhaHangLDP.Models;
using NhaHangLDP.Filters;

namespace NhaHangLDP.Controllers
{
    /// <summary>
    /// Controller quản lý phân quyền cho các tài khoản
    /// </summary>
    [CustomAuthorize("Admin")]
    public class PermissionController : Controller
    {
        private readonly NhaHangLDPEntities _db = new NhaHangLDPEntities();

        /// <summary>
        /// Danh sách trang/chức năng có thể phân quyền
        /// </summary>
        private List<PagePermissionItem> GetAvailablePages()
        {
            return new List<PagePermissionItem>
            {
                new PagePermissionItem { Controller = "Management", ActionName = "Menu", DisplayName = "Quản lý thực đơn", Icon = "restaurant_menu", Category = "Quản lý" },
                new PagePermissionItem { Controller = "Management", ActionName = "Tables", DisplayName = "Quản lý bàn", Icon = "table_restaurant", Category = "Quản lý" },
                new PagePermissionItem { Controller = "Kitchen", ActionName = "Index", DisplayName = "Quản lý bếp", Icon = "soup_kitchen", Category = "Vận hành" },
                new PagePermissionItem { Controller = "Delivery", ActionName = "Index", DisplayName = "Quản lý giao hàng", Icon = "delivery_dining", Category = "Vận hành" },
                new PagePermissionItem { Controller = "Payment", ActionName = "Index", DisplayName = "Quản lý thanh toán", Icon = "payments", Category = "Tài chính" },
                new PagePermissionItem { Controller = "HR", ActionName = "Index", DisplayName = "Quản lý nhân sự", Icon = "groups", Category = "Nhân sự" },
                new PagePermissionItem { Controller = "ReportsManagement", ActionName = "Index", DisplayName = "Báo cáo & thống kê", Icon = "analytics", Category = "Tài chính" },
                new PagePermissionItem { Controller = "Settings", ActionName = "Index", DisplayName = "Cài đặt hệ thống", Icon = "settings", Category = "Hệ thống" },
                new PagePermissionItem { Controller = "Promotion", ActionName = "Index", DisplayName = "Quản lý khuyến mãi", Icon = "local_offer", Category = "Marketing" },
                new PagePermissionItem { Controller = "QRCode", ActionName = "Index", DisplayName = "Quản lý QR Code", Icon = "qr_code_2", Category = "Vận hành" },
                new PagePermissionItem { Controller = "Email", ActionName = "Index", DisplayName = "Quản lý Email", Icon = "email", Category = "Marketing" },
                new PagePermissionItem { Controller = "Permission", ActionName = "Index", DisplayName = "Quản lý phân quyền", Icon = "admin_panel_settings", Category = "Hệ thống" }
            };
        }

        /// <summary>
        /// Trang quản lý phân quyền
        /// </summary>
        public ActionResult Index()
        {
            var roles = _db.Role.ToList();
            var availablePages = GetAvailablePages();

            var rolePermissions = new List<RolePermissionViewModel>();

            foreach (var role in roles)
            {
                var permissions = new List<PermissionItem>();

                try
                {
                    permissions = _db.Database.SqlQuery<PermissionItem>(
                        @"SELECT Id, RoleId, Controller, ActionName, Description, CanView, CanAdd, CanEdit, CanDelete
                          FROM RolePermission WHERE RoleId = @p0", role.Id).ToList();
                }
                catch
                {
                    // Table may not exist yet
                }

                var accountCount = _db.Account.Count(a => a.RoleId == role.Id);

                rolePermissions.Add(new RolePermissionViewModel
                {
                    RoleId = role.Id,
                    RoleName = role.RoleName,
                    AccountCount = accountCount,
                    Permissions = permissions
                });
            }

            var viewModel = new PermissionManagementViewModel
            {
                Roles = rolePermissions,
                AvailablePages = availablePages
            };

            return View(viewModel);
        }

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

                // Check if permission exists
                var existingCount = _db.Database.SqlQuery<int>(
                    "SELECT COUNT(*) FROM RolePermission WHERE RoleId = @p0 AND Controller = @p1 AND ActionName = @p2",
                    dto.RoleId, dto.Controller, dto.ActionName).FirstOrDefault();

                if (existingCount > 0)
                {
                    // Update existing
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
                    // Insert new
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
                var permissions = _db.Database.SqlQuery<PermissionItem>(
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

                // Delete all existing permissions for this role
                _db.Database.ExecuteSqlCommand(
                    "DELETE FROM RolePermission WHERE RoleId = @p0", roleId);

                // Insert new permissions
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
