using System;
using System.Collections.Generic;

namespace NhaHangLDP.Models
{
    /// <summary>
    /// ViewModel cho trang quản lý phân quyền
    /// </summary>
    public class PermissionManagementViewModel
    {
        public List<RolePermissionViewModel> Roles { get; set; }
        public List<PagePermissionItem> AvailablePages { get; set; }
    }

    /// <summary>
    /// Thông tin quyền của từng role
    /// </summary>
    public class RolePermissionViewModel
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; }
        public int AccountCount { get; set; }
        public List<PermissionItem> Permissions { get; set; }
    }

    /// <summary>
    /// Chi tiết quyền cho 1 trang/chức năng
    /// </summary>
    public class PermissionItem
    {
        public int Id { get; set; }
        public int RoleId { get; set; }
        public string Controller { get; set; }
        public string ActionName { get; set; }
        public string Description { get; set; }
        public bool CanView { get; set; }
        public bool CanAdd { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
    }

    /// <summary>
    /// Danh sách trang/chức năng có thể phân quyền
    /// </summary>
    public class PagePermissionItem
    {
        public string Controller { get; set; }
        public string ActionName { get; set; }
        public string DisplayName { get; set; }
        public string Icon { get; set; }
        public string Category { get; set; }
    }

    /// <summary>
    /// DTO cho cập nhật quyền
    /// </summary>
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
}
