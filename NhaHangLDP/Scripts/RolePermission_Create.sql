-- =============================================
-- Script: RolePermission_Create.sql
-- Mô tả: Tạo bảng RolePermission để quản lý phân quyền cho các tài khoản
-- Hướng dẫn: Chạy script này trong SQL Server Management Studio
-- =============================================

-- Tạo bảng RolePermission
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='RolePermission' AND xtype='U')
BEGIN
    CREATE TABLE RolePermission (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        RoleId INT NOT NULL,
        Controller NVARCHAR(100) NOT NULL,
        ActionName NVARCHAR(100) NOT NULL,
        CanView BIT NOT NULL DEFAULT 0,
        CanAdd BIT NOT NULL DEFAULT 0,
        CanEdit BIT NOT NULL DEFAULT 0,
        CanDelete BIT NOT NULL DEFAULT 0,
        Description NVARCHAR(255) NULL,
        CreatedDate DATETIME NOT NULL DEFAULT GETDATE(),
        UpdatedDate DATETIME NULL,
        CONSTRAINT FK_RolePermission_Role FOREIGN KEY (RoleId) REFERENCES Role(Id),
        CONSTRAINT UQ_RolePermission_Role_Controller_Action UNIQUE (RoleId, Controller, ActionName)
    );

    PRINT N'Đã tạo bảng RolePermission thành công!';
END
ELSE
BEGIN
    PRINT N'Bảng RolePermission đã tồn tại.';
END
GO

-- Thêm dữ liệu mặc định cho Admin (có tất cả quyền)
-- Lấy RoleId của Admin
DECLARE @AdminRoleId INT;
SELECT @AdminRoleId = Id FROM Role WHERE RoleName = 'Admin';

IF @AdminRoleId IS NOT NULL
BEGIN
    -- Quản lý thực đơn
    IF NOT EXISTS (SELECT 1 FROM RolePermission WHERE RoleId = @AdminRoleId AND Controller = 'Management' AND ActionName = 'Menu')
        INSERT INTO RolePermission (RoleId, Controller, ActionName, CanView, CanAdd, CanEdit, CanDelete, Description)
        VALUES (@AdminRoleId, 'Management', 'Menu', 1, 1, 1, 1, N'Quản lý thực đơn');

    -- Quản lý bàn
    IF NOT EXISTS (SELECT 1 FROM RolePermission WHERE RoleId = @AdminRoleId AND Controller = 'Management' AND ActionName = 'Tables')
        INSERT INTO RolePermission (RoleId, Controller, ActionName, CanView, CanAdd, CanEdit, CanDelete, Description)
        VALUES (@AdminRoleId, 'Management', 'Tables', 1, 1, 1, 1, N'Quản lý bàn');

    -- Quản lý đơn hàng
    IF NOT EXISTS (SELECT 1 FROM RolePermission WHERE RoleId = @AdminRoleId AND Controller = 'Kitchen' AND ActionName = 'Index')
        INSERT INTO RolePermission (RoleId, Controller, ActionName, CanView, CanAdd, CanEdit, CanDelete, Description)
        VALUES (@AdminRoleId, 'Kitchen', 'Index', 1, 1, 1, 1, N'Quản lý đơn hàng bếp');

    -- Quản lý giao hàng
    IF NOT EXISTS (SELECT 1 FROM RolePermission WHERE RoleId = @AdminRoleId AND Controller = 'Delivery' AND ActionName = 'Index')
        INSERT INTO RolePermission (RoleId, Controller, ActionName, CanView, CanAdd, CanEdit, CanDelete, Description)
        VALUES (@AdminRoleId, 'Delivery', 'Index', 1, 1, 1, 1, N'Quản lý giao hàng');

    -- Thanh toán
    IF NOT EXISTS (SELECT 1 FROM RolePermission WHERE RoleId = @AdminRoleId AND Controller = 'Payment' AND ActionName = 'Index')
        INSERT INTO RolePermission (RoleId, Controller, ActionName, CanView, CanAdd, CanEdit, CanDelete, Description)
        VALUES (@AdminRoleId, 'Payment', 'Index', 1, 1, 1, 1, N'Quản lý thanh toán');

    -- Nhân sự
    IF NOT EXISTS (SELECT 1 FROM RolePermission WHERE RoleId = @AdminRoleId AND Controller = 'HR' AND ActionName = 'Index')
        INSERT INTO RolePermission (RoleId, Controller, ActionName, CanView, CanAdd, CanEdit, CanDelete, Description)
        VALUES (@AdminRoleId, 'HR', 'Index', 1, 1, 1, 1, N'Quản lý nhân sự');

    -- Báo cáo
    IF NOT EXISTS (SELECT 1 FROM RolePermission WHERE RoleId = @AdminRoleId AND Controller = 'ReportsManagement' AND ActionName = 'Index')
        INSERT INTO RolePermission (RoleId, Controller, ActionName, CanView, CanAdd, CanEdit, CanDelete, Description)
        VALUES (@AdminRoleId, 'ReportsManagement', 'Index', 1, 1, 1, 1, N'Báo cáo & thống kê');

    -- Cài đặt
    IF NOT EXISTS (SELECT 1 FROM RolePermission WHERE RoleId = @AdminRoleId AND Controller = 'Settings' AND ActionName = 'Index')
        INSERT INTO RolePermission (RoleId, Controller, ActionName, CanView, CanAdd, CanEdit, CanDelete, Description)
        VALUES (@AdminRoleId, 'Settings', 'Index', 1, 1, 1, 1, N'Cài đặt hệ thống');

    -- Phân quyền
    IF NOT EXISTS (SELECT 1 FROM RolePermission WHERE RoleId = @AdminRoleId AND Controller = 'Permission' AND ActionName = 'Index')
        INSERT INTO RolePermission (RoleId, Controller, ActionName, CanView, CanAdd, CanEdit, CanDelete, Description)
        VALUES (@AdminRoleId, 'Permission', 'Index', 1, 1, 1, 1, N'Quản lý phân quyền');

    -- Khuyến mãi
    IF NOT EXISTS (SELECT 1 FROM RolePermission WHERE RoleId = @AdminRoleId AND Controller = 'Promotion' AND ActionName = 'Index')
        INSERT INTO RolePermission (RoleId, Controller, ActionName, CanView, CanAdd, CanEdit, CanDelete, Description)
        VALUES (@AdminRoleId, 'Promotion', 'Index', 1, 1, 1, 1, N'Quản lý khuyến mãi');

    -- QR Code
    IF NOT EXISTS (SELECT 1 FROM RolePermission WHERE RoleId = @AdminRoleId AND Controller = 'QRCode' AND ActionName = 'Index')
        INSERT INTO RolePermission (RoleId, Controller, ActionName, CanView, CanAdd, CanEdit, CanDelete, Description)
        VALUES (@AdminRoleId, 'QRCode', 'Index', 1, 1, 1, 1, N'Quản lý QR Code');

    -- Email
    IF NOT EXISTS (SELECT 1 FROM RolePermission WHERE RoleId = @AdminRoleId AND Controller = 'Email' AND ActionName = 'Index')
        INSERT INTO RolePermission (RoleId, Controller, ActionName, CanView, CanAdd, CanEdit, CanDelete, Description)
        VALUES (@AdminRoleId, 'Email', 'Index', 1, 1, 1, 1, N'Quản lý Email');

    PRINT N'Đã thêm quyền mặc định cho Admin.';
END
GO

-- Thêm dữ liệu mặc định cho Manager
DECLARE @ManagerRoleId INT;
SELECT @ManagerRoleId = Id FROM Role WHERE RoleName = 'Manager';

IF @ManagerRoleId IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM RolePermission WHERE RoleId = @ManagerRoleId AND Controller = 'Management' AND ActionName = 'Menu')
        INSERT INTO RolePermission (RoleId, Controller, ActionName, CanView, CanAdd, CanEdit, CanDelete, Description)
        VALUES (@ManagerRoleId, 'Management', 'Menu', 1, 1, 1, 0, N'Quản lý thực đơn');

    IF NOT EXISTS (SELECT 1 FROM RolePermission WHERE RoleId = @ManagerRoleId AND Controller = 'Management' AND ActionName = 'Tables')
        INSERT INTO RolePermission (RoleId, Controller, ActionName, CanView, CanAdd, CanEdit, CanDelete, Description)
        VALUES (@ManagerRoleId, 'Management', 'Tables', 1, 1, 1, 0, N'Quản lý bàn');

    IF NOT EXISTS (SELECT 1 FROM RolePermission WHERE RoleId = @ManagerRoleId AND Controller = 'Kitchen' AND ActionName = 'Index')
        INSERT INTO RolePermission (RoleId, Controller, ActionName, CanView, CanAdd, CanEdit, CanDelete, Description)
        VALUES (@ManagerRoleId, 'Kitchen', 'Index', 1, 1, 1, 0, N'Quản lý đơn hàng bếp');

    IF NOT EXISTS (SELECT 1 FROM RolePermission WHERE RoleId = @ManagerRoleId AND Controller = 'Delivery' AND ActionName = 'Index')
        INSERT INTO RolePermission (RoleId, Controller, ActionName, CanView, CanAdd, CanEdit, CanDelete, Description)
        VALUES (@ManagerRoleId, 'Delivery', 'Index', 1, 1, 1, 0, N'Quản lý giao hàng');

    IF NOT EXISTS (SELECT 1 FROM RolePermission WHERE RoleId = @ManagerRoleId AND Controller = 'Payment' AND ActionName = 'Index')
        INSERT INTO RolePermission (RoleId, Controller, ActionName, CanView, CanAdd, CanEdit, CanDelete, Description)
        VALUES (@ManagerRoleId, 'Payment', 'Index', 1, 1, 1, 0, N'Quản lý thanh toán');

    IF NOT EXISTS (SELECT 1 FROM RolePermission WHERE RoleId = @ManagerRoleId AND Controller = 'HR' AND ActionName = 'Index')
        INSERT INTO RolePermission (RoleId, Controller, ActionName, CanView, CanAdd, CanEdit, CanDelete, Description)
        VALUES (@ManagerRoleId, 'HR', 'Index', 1, 1, 1, 0, N'Quản lý nhân sự');

    IF NOT EXISTS (SELECT 1 FROM RolePermission WHERE RoleId = @ManagerRoleId AND Controller = 'ReportsManagement' AND ActionName = 'Index')
        INSERT INTO RolePermission (RoleId, Controller, ActionName, CanView, CanAdd, CanEdit, CanDelete, Description)
        VALUES (@ManagerRoleId, 'ReportsManagement', 'Index', 1, 0, 0, 0, N'Báo cáo & thống kê');

    IF NOT EXISTS (SELECT 1 FROM RolePermission WHERE RoleId = @ManagerRoleId AND Controller = 'Promotion' AND ActionName = 'Index')
        INSERT INTO RolePermission (RoleId, Controller, ActionName, CanView, CanAdd, CanEdit, CanDelete, Description)
        VALUES (@ManagerRoleId, 'Promotion', 'Index', 1, 1, 1, 0, N'Quản lý khuyến mãi');

    PRINT N'Đã thêm quyền mặc định cho Manager.';
END
GO

-- Thêm dữ liệu mặc định cho Cashier
DECLARE @CashierRoleId INT;
SELECT @CashierRoleId = Id FROM Role WHERE RoleName = 'Cashier';

IF @CashierRoleId IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM RolePermission WHERE RoleId = @CashierRoleId AND Controller = 'Payment' AND ActionName = 'Index')
        INSERT INTO RolePermission (RoleId, Controller, ActionName, CanView, CanAdd, CanEdit, CanDelete, Description)
        VALUES (@CashierRoleId, 'Payment', 'Index', 1, 1, 1, 0, N'Quản lý thanh toán');

    IF NOT EXISTS (SELECT 1 FROM RolePermission WHERE RoleId = @CashierRoleId AND Controller = 'Kitchen' AND ActionName = 'Index')
        INSERT INTO RolePermission (RoleId, Controller, ActionName, CanView, CanAdd, CanEdit, CanDelete, Description)
        VALUES (@CashierRoleId, 'Kitchen', 'Index', 1, 0, 1, 0, N'Xem đơn hàng bếp');

    PRINT N'Đã thêm quyền mặc định cho Cashier.';
END
GO

-- Thêm dữ liệu mặc định cho Delivery
DECLARE @DeliveryRoleId INT;
SELECT @DeliveryRoleId = Id FROM Role WHERE RoleName = 'Delivery';

IF @DeliveryRoleId IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM RolePermission WHERE RoleId = @DeliveryRoleId AND Controller = 'Delivery' AND ActionName = 'Index')
        INSERT INTO RolePermission (RoleId, Controller, ActionName, CanView, CanAdd, CanEdit, CanDelete, Description)
        VALUES (@DeliveryRoleId, 'Delivery', 'Index', 1, 0, 1, 0, N'Quản lý giao hàng');

    PRINT N'Đã thêm quyền mặc định cho Delivery.';
END
GO

PRINT N'=== Hoàn tất tạo bảng RolePermission và dữ liệu mặc định! ===';
GO
