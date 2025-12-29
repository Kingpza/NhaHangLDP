using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace NhaHangLDP.Models;

public partial class MyDbContext : DbContext
{
    public MyDbContext()
    {
    }

    public MyDbContext(DbContextOptions<MyDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Account> Accounts { get; set; }

    public virtual DbSet<AppSetting> AppSettings { get; set; }

    public virtual DbSet<Attendance> Attendances { get; set; }

    public virtual DbSet<Bill> Bills { get; set; }

    public virtual DbSet<Booking> Bookings { get; set; }

    public virtual DbSet<Cart> Carts { get; set; }

    public virtual DbSet<CartItem> CartItems { get; set; }

    public virtual DbSet<CashierShift> CashierShifts { get; set; }

    public virtual DbSet<Customer> Customers { get; set; }

    public virtual DbSet<CustomerAddress> CustomerAddresses { get; set; }

    public virtual DbSet<CustomerNotification> CustomerNotifications { get; set; }

    public virtual DbSet<CustomerOrder> CustomerOrders { get; set; }

    public virtual DbSet<CustomerOrderDetail> CustomerOrderDetails { get; set; }

    public virtual DbSet<DamagedStock> DamagedStocks { get; set; }

    public virtual DbSet<DeliveryAssignment> DeliveryAssignments { get; set; }

    public virtual DbSet<DeliverySetting> DeliverySettings { get; set; }

    public virtual DbSet<DeliveryZone> DeliveryZones { get; set; }

    public virtual DbSet<EmailConfig> EmailConfigs { get; set; }

    public virtual DbSet<EmailLog> EmailLogs { get; set; }

    public virtual DbSet<EmailTemplate> EmailTemplates { get; set; }

    public virtual DbSet<Employee> Employees { get; set; }

    public virtual DbSet<EmployeeContract> EmployeeContracts { get; set; }

    public virtual DbSet<EmployeeSchedule> EmployeeSchedules { get; set; }

    public virtual DbSet<Ingredient> Ingredients { get; set; }

    public virtual DbSet<KitchenOrderItem> KitchenOrderItems { get; set; }

    public virtual DbSet<KitchenOrderTicket> KitchenOrderTickets { get; set; }

    public virtual DbSet<KitchenStation> KitchenStations { get; set; }

    public virtual DbSet<LeaveRequest> LeaveRequests { get; set; }

    public virtual DbSet<MenuCombo> MenuCombos { get; set; }

    public virtual DbSet<MenuComboItem> MenuComboItems { get; set; }

    public virtual DbSet<MenuItem> MenuItems { get; set; }

    public virtual DbSet<MenuItemIngredient> MenuItemIngredients { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderDetail> OrderDetails { get; set; }

    public virtual DbSet<Payroll> Payrolls { get; set; }

    public virtual DbSet<PerformanceReview> PerformanceReviews { get; set; }

    public virtual DbSet<PriceHistory> PriceHistories { get; set; }

    public virtual DbSet<Promotion> Promotions { get; set; }

    public virtual DbSet<PromotionUsage> PromotionUsages { get; set; }

    public virtual DbSet<QROrder> QROrders { get; set; }

    public virtual DbSet<QROrderDetail> QROrderDetails { get; set; }

    public virtual DbSet<Reservation> Reservations { get; set; }

    public virtual DbSet<RestaurantTable> RestaurantTables { get; set; }

    public virtual DbSet<ReturnBill> ReturnBills { get; set; }

    public virtual DbSet<ReturnBillDetail> ReturnBillDetails { get; set; }

    public virtual DbSet<Review> Reviews { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<ShiftSupportStaff> ShiftSupportStaffs { get; set; }

    public virtual DbSet<Shipper> Shippers { get; set; }

    public virtual DbSet<StockInbound> StockInbounds { get; set; }

    public virtual DbSet<StockInboundDetail> StockInboundDetails { get; set; }

    public virtual DbSet<Supplier> Suppliers { get; set; }

    public virtual DbSet<TableArea> TableAreas { get; set; }

    public virtual DbSet<TableSession> TableSessions { get; set; }

    public virtual DbSet<VW_AccountInfo> VW_AccountInfos { get; set; }

    public virtual DbSet<VW_AppSetting> VW_AppSettings { get; set; }

    public virtual DbSet<Voucher> Vouchers { get; set; }

    public virtual DbSet<VoucherUsage> VoucherUsages { get; set; }

    public virtual DbSet<Wishlist> Wishlists { get; set; }

    public virtual DbSet<WorkShift> WorkShifts { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Connection string is configured via Program.cs using dependency injection
        if (!optionsBuilder.IsConfigured)
        {
            // Fallback only for design-time tools
            optionsBuilder.UseSqlServer("Name=ConnectionStrings:DefaultConnection");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Account__3214EC07F4C9FDAD");

            entity.ToTable("Account");

            entity.HasIndex(e => e.IsActive, "IX_Account_IsActive");

            entity.HasIndex(e => e.RoleId, "IX_Account_RoleId");

            entity.HasIndex(e => e.Username, "IX_Account_Username");

            entity.HasIndex(e => e.Username, "UQ__Account__536C85E47F008A0A").IsUnique();

            entity.Property(e => e.CreatedBy).HasMaxLength(50);
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.FullName)
                .IsRequired()
                .HasMaxLength(100);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.LastLoginDate).HasColumnType("datetime");
            entity.Property(e => e.PasswordHash)
                .IsRequired()
                .HasMaxLength(255);
            entity.Property(e => e.PhoneNumber).HasMaxLength(15);
            entity.Property(e => e.UpdatedBy).HasMaxLength(50);
            entity.Property(e => e.UpdatedDate).HasColumnType("datetime");
            entity.Property(e => e.Username)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasOne(d => d.Role).WithMany(p => p.Accounts)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Account_Role");
        });

        modelBuilder.Entity<AppSetting>(entity =>
        {
            entity.HasKey(e => e.SettingKey);

            entity.ToTable("AppSetting");

            entity.HasIndex(e => e.CreatedDate, "IX_AppSetting_CreatedDate");

            entity.Property(e => e.SettingKey).HasMaxLength(100);
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.SettingValue)
                .IsRequired()
                .HasMaxLength(1000);
            entity.Property(e => e.UpdatedBy).HasMaxLength(100);
            entity.Property(e => e.UpdatedDate).HasColumnType("datetime");
        });

        modelBuilder.Entity<Attendance>(entity =>
        {
            entity.ToTable("Attendance");

            entity.HasIndex(e => new { e.EmployeeId, e.CheckInTime }, "IX_Attendance_EmployeeId_CheckInTime");

            entity.Property(e => e.CheckInTime).HasColumnType("datetime");
            entity.Property(e => e.CheckOutTime).HasColumnType("datetime");
            entity.Property(e => e.Location).HasMaxLength(100);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.WorkHours).HasColumnType("decimal(5, 2)");

            entity.HasOne(d => d.Employee).WithMany(p => p.Attendances)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Attendance_Employee");
        });

        modelBuilder.Entity<Bill>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Bill__3214EC073F9C57FC");

            entity.ToTable("Bill");

            entity.Property(e => e.BillDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.FinalAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.PaymentMethod)
                .IsRequired()
                .HasMaxLength(100);
            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("Paid");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Cashier).WithMany(p => p.Bills)
                .HasForeignKey(d => d.CashierId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Bill_Employee_Cashier");

            entity.HasOne(d => d.Orders).WithMany(p => p.Bills)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Bill_Order");
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Booking__3214EC07FE2DA536");

            entity.ToTable("Booking");

            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.CustomerName)
                .IsRequired()
                .HasMaxLength(255);
            entity.Property(e => e.CustomerPhone)
                .IsRequired()
                .HasMaxLength(20);
            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("Confirmed");

            entity.HasOne(d => d.Table).WithMany(p => p.Bookings)
                .HasForeignKey(d => d.TableId)
                .HasConstraintName("FK_Booking_RestaurantTable");
        });

        modelBuilder.Entity<Cart>(entity =>
        {
            entity.ToTable("Cart");

            entity.HasIndex(e => e.SessionId, "IX_Cart_SessionId");

            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.SessionId).HasMaxLength(100);
            entity.Property(e => e.UpdatedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Customer).WithMany(p => p.Carts)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_Cart_Customer");
        });

        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.ToTable("CartItem");

            entity.Property(e => e.AddedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Quantity).HasDefaultValue(1);
            entity.Property(e => e.SpecialInstructions).HasMaxLength(500);
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Cart).WithMany(p => p.CartItems)
                .HasForeignKey(d => d.CartId)
                .HasConstraintName("FK_CartItem_Cart");

            entity.HasOne(d => d.MenuItem).WithMany(p => p.CartItems)
                .HasForeignKey(d => d.MenuItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CartItem_MenuItem");
        });

        modelBuilder.Entity<CashierShift>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__CashierS__3214EC075A203EB2");

            entity.ToTable("CashierShift");

            entity.Property(e => e.FinalCash).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.InitialCash).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("Open");
            entity.Property(e => e.TotalRevenue).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Cashier).WithMany(p => p.CashierShifts)
                .HasForeignKey(d => d.CashierId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CashierShift_Employee");
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customer");

            entity.HasIndex(e => e.Email, "UQ_Customer_Email").IsUnique();

            entity.Property(e => e.AvatarUrl).HasMaxLength(500);
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Email)
                .IsRequired()
                .HasMaxLength(100);
            entity.Property(e => e.FullName)
                .IsRequired()
                .HasMaxLength(100);
            entity.Property(e => e.Gender).HasMaxLength(10);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.LastLoginDate).HasColumnType("datetime");
            entity.Property(e => e.MembershipLevel)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("Bronze");
            entity.Property(e => e.PasswordHash).HasMaxLength(256);
            entity.Property(e => e.Phone).HasMaxLength(20);
        });

        modelBuilder.Entity<CustomerAddress>(entity =>
        {
            entity.ToTable("CustomerAddress");

            entity.Property(e => e.AddressLine)
                .IsRequired()
                .HasMaxLength(500);
            entity.Property(e => e.AddressType)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("Home");
            entity.Property(e => e.City).HasMaxLength(100);
            entity.Property(e => e.District).HasMaxLength(100);
            entity.Property(e => e.Latitude).HasColumnType("decimal(10, 7)");
            entity.Property(e => e.Longitude).HasColumnType("decimal(10, 7)");
            entity.Property(e => e.ReceiverName)
                .IsRequired()
                .HasMaxLength(100);
            entity.Property(e => e.ReceiverPhone)
                .IsRequired()
                .HasMaxLength(20);
            entity.Property(e => e.Ward).HasMaxLength(100);

            entity.HasOne(d => d.Customer).WithMany(p => p.CustomerAddresses)
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("FK_CustomerAddress_Customer");
        });

        modelBuilder.Entity<CustomerNotification>(entity =>
        {
            entity.ToTable("CustomerNotification");

            entity.Property(e => e.ActionUrl).HasMaxLength(500);
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.ImageUrl).HasMaxLength(200);
            entity.Property(e => e.Message).HasMaxLength(1000);
            entity.Property(e => e.ReadDate).HasColumnType("datetime");
            entity.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(200);
            entity.Property(e => e.Type)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("General");

            entity.HasOne(d => d.Customer).WithMany(p => p.CustomerNotifications)
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("FK_CustomerNotification_Customer");
        });

        modelBuilder.Entity<CustomerOrder>(entity =>
        {
            entity.ToTable("CustomerOrder");

            entity.HasIndex(e => e.CustomerId, "IX_CustomerOrder_CustomerId");

            entity.HasIndex(e => e.OrderDate, "IX_CustomerOrder_OrderDate").IsDescending();

            entity.HasIndex(e => e.Status, "IX_CustomerOrder_Status");

            entity.HasIndex(e => e.OrderCode, "UQ_CustomerOrder_Code").IsUnique();

            entity.Property(e => e.CancelReason).HasMaxLength(500);
            entity.Property(e => e.CancelledDate).HasColumnType("datetime");
            entity.Property(e => e.City).HasMaxLength(100);
            entity.Property(e => e.CompletedDate).HasColumnType("datetime");
            entity.Property(e => e.ConfirmedDate).HasColumnType("datetime");
            entity.Property(e => e.CustomerEmail).HasMaxLength(100);
            entity.Property(e => e.CustomerName)
                .IsRequired()
                .HasMaxLength(100);
            entity.Property(e => e.CustomerPhone)
                .IsRequired()
                .HasMaxLength(20);
            entity.Property(e => e.DeliveringDate).HasColumnType("datetime");
            entity.Property(e => e.DeliveryAddress).HasMaxLength(500);
            entity.Property(e => e.DeliveryFee).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Discount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.District).HasMaxLength(100);
            entity.Property(e => e.EstimatedDeliveryTime).HasColumnType("datetime");
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.OrderCode)
                .IsRequired()
                .HasMaxLength(20);
            entity.Property(e => e.OrderDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.OrderType)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("Delivery");
            entity.Property(e => e.PaidDate).HasColumnType("datetime");
            entity.Property(e => e.PaymentMethod)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("COD");
            entity.Property(e => e.PaymentStatus)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("Pending");
            entity.Property(e => e.PreparingDate).HasColumnType("datetime");
            entity.Property(e => e.ReadyDate).HasColumnType("datetime");
            entity.Property(e => e.ReviewComment).HasMaxLength(500);
            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("Pending");
            entity.Property(e => e.SubTotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TransactionId).HasMaxLength(100);
            entity.Property(e => e.VoucherCode).HasMaxLength(50);
            entity.Property(e => e.Ward).HasMaxLength(100);

            entity.HasOne(d => d.Customer).WithMany(p => p.CustomerOrders)
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("FK_CustomerOrder_Customer");
        });

        modelBuilder.Entity<CustomerOrderDetail>(entity =>
        {
            entity.ToTable("CustomerOrderDetail");

            entity.Property(e => e.ItemName)
                .IsRequired()
                .HasMaxLength(200);
            entity.Property(e => e.SpecialInstructions).HasMaxLength(500);
            entity.Property(e => e.Subtotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.CustomerOrder).WithMany(p => p.CustomerOrderDetails)
                .HasForeignKey(d => d.CustomerOrderId)
                .HasConstraintName("FK_CustomerOrderDetail_Order");

            entity.HasOne(d => d.MenuItem).WithMany(p => p.CustomerOrderDetails)
                .HasForeignKey(d => d.MenuItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CustomerOrderDetail_MenuItem");
        });

        modelBuilder.Entity<DamagedStock>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__DamagedS__3214EC07379B7EC2");

            entity.ToTable("DamagedStock");

            entity.Property(e => e.DamageDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Reason).IsRequired();

            entity.HasOne(d => d.Ingredient).WithMany(p => p.DamagedStocks)
                .HasForeignKey(d => d.IngredientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DamagedStock_Ingredient");

            entity.HasOne(d => d.ReportedByEmployee).WithMany(p => p.DamagedStocks)
                .HasForeignKey(d => d.ReportedByEmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DamagedStock_Employee");
        });

        modelBuilder.Entity<DeliveryAssignment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Delivery__3214EC07C2E334DB");

            entity.ToTable("DeliveryAssignment");

            entity.HasIndex(e => e.OrderId, "IX_DeliveryAssignment_OrderId");

            entity.HasIndex(e => e.ShipperId, "IX_DeliveryAssignment_ShipperId");

            entity.HasIndex(e => e.Status, "IX_DeliveryAssignment_Status");

            entity.Property(e => e.ActualDistance).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.AssignedTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.CurrentLatitude).HasColumnType("decimal(10, 8)");
            entity.Property(e => e.CurrentLongitude).HasColumnType("decimal(11, 8)");
            entity.Property(e => e.CustomerFeedback).HasMaxLength(500);
            entity.Property(e => e.DeliveryFee).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DeliveryTime).HasColumnType("datetime");
            entity.Property(e => e.EstimatedArrival).HasColumnType("datetime");
            entity.Property(e => e.FailureReason).HasMaxLength(500);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.PickupTime).HasColumnType("datetime");
            entity.Property(e => e.ProofImageUrl).HasMaxLength(500);
            entity.Property(e => e.ShipperEarning).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("Assigned");

            entity.HasOne(d => d.Orders).WithMany(p => p.DeliveryAssignments)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DeliveryAssignment_CustomerOrder");

            entity.HasOne(d => d.Shipper).WithMany(p => p.DeliveryAssignments)
                .HasForeignKey(d => d.ShipperId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_DeliveryAssignment_Shipper");
        });

        modelBuilder.Entity<DeliverySetting>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Delivery__3214EC07795619FA");

            entity.HasIndex(e => e.SettingKey, "UQ__Delivery__01E719AD99448807").IsUnique();

            entity.Property(e => e.Description).HasMaxLength(200);
            entity.Property(e => e.SettingKey)
                .IsRequired()
                .HasMaxLength(50);
            entity.Property(e => e.SettingValue).HasMaxLength(500);
            entity.Property(e => e.UpdatedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
        });

        modelBuilder.Entity<DeliveryZone>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Delivery__3214EC07F1673228");

            entity.ToTable("DeliveryZone");

            entity.Property(e => e.DeliveryFee)
                .HasDefaultValue(25000m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.District).HasMaxLength(100);
            entity.Property(e => e.EstimatedTime).HasDefaultValue(30);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.MaxDistance).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.MinOrderForFreeDelivery).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Ward).HasMaxLength(100);
            entity.Property(e => e.ZoneName)
                .IsRequired()
                .HasMaxLength(100);
        });

        modelBuilder.Entity<EmailConfig>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__EmailCon__3214EC0740231DCA");

            entity.ToTable("EmailConfig");

            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.DefaultCc).HasMaxLength(500);
            entity.Property(e => e.EnableSsl).HasDefaultValue(true);
            entity.Property(e => e.FromEmail)
                .IsRequired()
                .HasMaxLength(200);
            entity.Property(e => e.FromName)
                .IsRequired()
                .HasMaxLength(200);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.SmtpPassword)
                .IsRequired()
                .HasMaxLength(200);
            entity.Property(e => e.SmtpPort).HasDefaultValue(587);
            entity.Property(e => e.SmtpServer)
                .IsRequired()
                .HasMaxLength(200);
            entity.Property(e => e.SmtpUsername)
                .IsRequired()
                .HasMaxLength(200);
            entity.Property(e => e.UpdatedDate).HasColumnType("datetime");
        });

        modelBuilder.Entity<EmailLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__EmailLog__3214EC07EFF31C6B");

            entity.ToTable("EmailLog");

            entity.HasIndex(e => e.CreatedDate, "IX_EmailLog_Date").IsDescending();

            entity.HasIndex(e => e.Status, "IX_EmailLog_Status");

            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.EmailType)
                .IsRequired()
                .HasMaxLength(50);
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
            entity.Property(e => e.ReferenceType).HasMaxLength(50);
            entity.Property(e => e.SentDate).HasColumnType("datetime");
            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("Pending");
            entity.Property(e => e.Subject)
                .IsRequired()
                .HasMaxLength(500);
            entity.Property(e => e.ToEmail)
                .IsRequired()
                .HasMaxLength(200);
            entity.Property(e => e.ToName).HasMaxLength(100);
        });

        modelBuilder.Entity<EmailTemplate>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__EmailTem__3214EC07A10F9A94");

            entity.ToTable("EmailTemplate");

            entity.HasIndex(e => e.TemplateCode, "IX_EmailTemplate_Code");

            entity.HasIndex(e => e.TemplateCode, "UQ__EmailTem__0FDB50813E2A2BCF").IsUnique();

            entity.Property(e => e.AvailablePlaceholders).HasMaxLength(1000);
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.HtmlBody).IsRequired();
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);
            entity.Property(e => e.Subject)
                .IsRequired()
                .HasMaxLength(500);
            entity.Property(e => e.TemplateCode)
                .IsRequired()
                .HasMaxLength(50);
            entity.Property(e => e.UpdatedDate).HasColumnType("datetime");
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Employee__3214EC0782205D74");

            entity.ToTable("Employee");

            entity.HasIndex(e => e.UserName, "UQ__Employee__C9F28456BF1FBDDD").IsUnique();

            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.FullName)
                .IsRequired()
                .HasMaxLength(255);
            entity.Property(e => e.HireDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.PhoneNumber).HasMaxLength(20);
            entity.Property(e => e.UserName)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasOne(d => d.Role).WithMany(p => p.Employees)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Employee_Role");
        });

        modelBuilder.Entity<EmployeeContract>(entity =>
        {
            entity.ToTable("EmployeeContract");

            entity.Property(e => e.Allowance).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.BaseSalary).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ContractType)
                .IsRequired()
                .HasMaxLength(50);
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Active");

            entity.HasOne(d => d.Employee).WithMany(p => p.EmployeeContracts)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_EmployeeContract_Employee");
        });

        modelBuilder.Entity<EmployeeSchedule>(entity =>
        {
            entity.ToTable("EmployeeSchedule");

            entity.HasIndex(e => e.WorkDate, "IX_EmployeeSchedule_WorkDate");

            entity.Property(e => e.Note).HasMaxLength(200);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Scheduled");

            entity.HasOne(d => d.Employee).WithMany(p => p.EmployeeSchedules)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_EmployeeSchedule_Employee");

            entity.HasOne(d => d.WorkShift).WithMany(p => p.EmployeeSchedules)
                .HasForeignKey(d => d.WorkShiftId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_EmployeeSchedule_WorkShift");
        });

        modelBuilder.Entity<Ingredient>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Ingredie__3214EC07671460BF");

            entity.ToTable("Ingredient");

            entity.Property(e => e.AvailableStock).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.EstimatedCost).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.LowStockThreshold).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(255);
            entity.Property(e => e.Unit)
                .IsRequired()
                .HasMaxLength(50);
        });

        modelBuilder.Entity<KitchenOrderItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__KitchenO__3214EC07E029B376");

            entity.ToTable("KitchenOrderItem");

            entity.HasIndex(e => e.KitchenOrderTicketId, "IX_KitchenItem_Ticket");

            entity.Property(e => e.CompletedTime).HasColumnType("datetime");
            entity.Property(e => e.CustomerNotes).HasMaxLength(200);
            entity.Property(e => e.ItemName)
                .IsRequired()
                .HasMaxLength(200);
            entity.Property(e => e.KitchenNotes).HasMaxLength(200);
            entity.Property(e => e.Quantity).HasDefaultValue(1);
            entity.Property(e => e.StartedTime).HasColumnType("datetime");
            entity.Property(e => e.Station).HasMaxLength(50);
            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("Pending");

            entity.HasOne(d => d.KitchenOrderTicket).WithMany(p => p.KitchenOrderItems)
                .HasForeignKey(d => d.KitchenOrderTicketId)
                .HasConstraintName("FK_KitchenItem_Ticket");

            entity.HasOne(d => d.MenuItem).WithMany(p => p.KitchenOrderItems)
                .HasForeignKey(d => d.MenuItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_KitchenItem_MenuItem");
        });

        modelBuilder.Entity<KitchenOrderTicket>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__KitchenO__3214EC0784E263E8");

            entity.ToTable("KitchenOrderTicket");

            entity.HasIndex(e => e.OrderId, "IX_KitchenTicket_Order");

            entity.HasIndex(e => new { e.Status, e.CreatedTime }, "IX_KitchenTicket_Status");

            entity.HasIndex(e => e.TicketCode, "UQ__KitchenO__598CF7A36A345917").IsUnique();

            entity.Property(e => e.CompletedTime).HasColumnType("datetime");
            entity.Property(e => e.CreatedTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.EstimatedMinutes).HasDefaultValue(15);
            entity.Property(e => e.KitchenStation).HasMaxLength(50);
            entity.Property(e => e.Priority).HasDefaultValue(2);
            entity.Property(e => e.SpecialNotes).HasMaxLength(500);
            entity.Property(e => e.StartedTime).HasColumnType("datetime");
            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("New");
            entity.Property(e => e.TicketCode)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasOne(d => d.AssignedChef).WithMany(p => p.KitchenOrderTickets)
                .HasForeignKey(d => d.AssignedChefId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_KitchenTicket_Chef");

            entity.HasOne(d => d.Orders).WithMany(p => p.KitchenOrderTickets)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK_KitchenTicket_Order");

            entity.HasOne(d => d.Table).WithMany(p => p.KitchenOrderTickets)
                .HasForeignKey(d => d.TableId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_KitchenTicket_Table");
        });

        modelBuilder.Entity<KitchenStation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__KitchenS__3214EC07128E1B72");

            entity.ToTable("KitchenStation");

            entity.HasIndex(e => e.Code, "UQ__KitchenS__A25C5AA7D44A53E5").IsUnique();

            entity.Property(e => e.Code)
                .IsRequired()
                .HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(200);
            entity.Property(e => e.DisplayColor).HasMaxLength(20);
            entity.Property(e => e.HandledCategories).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(100);
        });

        modelBuilder.Entity<LeaveRequest>(entity =>
        {
            entity.ToTable("LeaveRequest");

            entity.HasIndex(e => new { e.EmployeeId, e.Status }, "IX_LeaveRequest_EmployeeId_Status");

            entity.Property(e => e.ApprovedDate).HasColumnType("datetime");
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.LeaveType)
                .IsRequired()
                .HasMaxLength(50);
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("Pending");

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.LeaveRequestApprovedByNavigations)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("FK_LeaveRequest_Approver");

            entity.HasOne(d => d.Employee).WithMany(p => p.LeaveRequestEmployees)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_LeaveRequest_Employee");
        });

        modelBuilder.Entity<MenuCombo>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__MenuComb__3214EC078F16E536");

            entity.ToTable("MenuCombo");

            entity.Property(e => e.ComboPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(255);
            entity.Property(e => e.TotalItemPrice).HasColumnType("decimal(18, 2)");
        });

        modelBuilder.Entity<MenuComboItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__MenuComb__3214EC079D678DE7");

            entity.ToTable("MenuComboItem");

            entity.Property(e => e.Quantity).HasDefaultValue(1);

            entity.HasOne(d => d.MenuCombo).WithMany(p => p.MenuComboItems)
                .HasForeignKey(d => d.MenuComboId)
                .HasConstraintName("FK_MenuComboItem_MenuCombo");

            entity.HasOne(d => d.MenuItem).WithMany(p => p.MenuComboItems)
                .HasForeignKey(d => d.MenuItemId)
                .HasConstraintName("FK_MenuComboItem_MenuItem");
        });

        modelBuilder.Entity<MenuItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__MenuItem__3214EC0749EF0C70");

            entity.ToTable("MenuItem");

            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.CreatedBy).HasMaxLength(255);
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsAvailable).HasDefaultValue(true);
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(255);
            entity.Property(e => e.OriginalPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.PreparationTime).HasDefaultValue(15);
            entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Rating).HasColumnType("decimal(3, 2)");
        });

        modelBuilder.Entity<MenuItemIngredient>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__MenuItem__3214EC075DE96EDC");

            entity.ToTable("MenuItemIngredient");

            entity.Property(e => e.RequiredQuantity).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Unit).HasMaxLength(50);

            entity.HasOne(d => d.Ingredient).WithMany(p => p.MenuItemIngredients)
                .HasForeignKey(d => d.IngredientId)
                .HasConstraintName("FK_MenuItemIngredient_Ingredient");

            entity.HasOne(d => d.MenuItem).WithMany(p => p.MenuItemIngredients)
                .HasForeignKey(d => d.MenuItemId)
                .HasConstraintName("FK_MenuItemIngredient_MenuItem");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Notifica__3214EC07C376BDF0");

            entity.ToTable("Notification");

            entity.HasIndex(e => e.CreatedDate, "IX_Notification_Date").IsDescending();

            entity.HasIndex(e => new { e.RecipientEmployeeId, e.IsRead }, "IX_Notification_Recipient");

            entity.HasIndex(e => new { e.RecipientRole, e.IsRead }, "IX_Notification_Role");

            entity.Property(e => e.ActionUrl).HasMaxLength(500);
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.ExpiryDate).HasColumnType("datetime");
            entity.Property(e => e.Level)
                .HasMaxLength(20)
                .HasDefaultValue("Info");
            entity.Property(e => e.Message)
                .IsRequired()
                .HasMaxLength(1000);
            entity.Property(e => e.ReadDate).HasColumnType("datetime");
            entity.Property(e => e.RecipientRole).HasMaxLength(50);
            entity.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(200);
            entity.Property(e => e.Type)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasOne(d => d.RecipientEmployee).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.RecipientEmployeeId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_Notification_Employee");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Order__3214EC0754D1AB4B");

            entity.ToTable("Order");

            entity.Property(e => e.OrderTime).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("Pending");

            entity.HasOne(d => d.Shift).WithMany(p => p.Orders)
                .HasForeignKey(d => d.ShiftId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Order_CashierShift");

            entity.HasOne(d => d.Table).WithMany(p => p.Orders)
                .HasForeignKey(d => d.TableId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Order_RestaurantTable");

            entity.HasOne(d => d.Waiter).WithMany(p => p.Orders)
                .HasForeignKey(d => d.WaiterId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Order_Employee_Waiter");
        });

        modelBuilder.Entity<OrderDetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__OrderDet__3214EC0792F4D358");

            entity.ToTable("OrderDetail");

            entity.Property(e => e.PriceAtTime).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.MenuItem).WithMany(p => p.OrderDetails)
                .HasForeignKey(d => d.MenuItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_OrderDetail_MenuItem");

            entity.HasOne(d => d.Orders).WithMany(p => p.OrderDetails)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK_OrderDetail_Order");
        });

        modelBuilder.Entity<Payroll>(entity =>
        {
            entity.ToTable("Payroll");

            entity.HasIndex(e => new { e.EmployeeId, e.Month, e.Year }, "IX_Payroll_EmployeeId_Month_Year");

            entity.Property(e => e.Allowance).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.BaseSalary).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Deduction).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.NetSalary).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.OvertimeBonus).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.PaymentDate).HasColumnType("datetime");
            entity.Property(e => e.PerformanceBonus).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Draft");
            entity.Property(e => e.TotalWorkDays).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TotalWorkHours).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Employee).WithMany(p => p.Payrolls)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Payroll_Employee");
        });

        modelBuilder.Entity<PerformanceReview>(entity =>
        {
            entity.ToTable("PerformanceReview");

            entity.Property(e => e.AreasToImprove).HasMaxLength(1000);
            entity.Property(e => e.Comments).HasMaxLength(1000);
            entity.Property(e => e.Communication).HasDefaultValue(3);
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.OverallScore).HasColumnType("decimal(3, 2)");
            entity.Property(e => e.Punctuality).HasDefaultValue(3);
            entity.Property(e => e.ReviewPeriod)
                .IsRequired()
                .HasMaxLength(50);
            entity.Property(e => e.ServiceQuality).HasDefaultValue(3);
            entity.Property(e => e.Strengths).HasMaxLength(1000);
            entity.Property(e => e.Teamwork).HasDefaultValue(3);
            entity.Property(e => e.WorkEfficiency).HasDefaultValue(3);

            entity.HasOne(d => d.Employee).WithMany(p => p.PerformanceReviewEmployees)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PerformanceReview_Employee");

            entity.HasOne(d => d.Reviewer).WithMany(p => p.PerformanceReviewReviewers)
                .HasForeignKey(d => d.ReviewerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PerformanceReview_Reviewer");
        });

        modelBuilder.Entity<PriceHistory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__PriceHis__3214EC07CD2F15F5");

            entity.ToTable("PriceHistory");

            entity.Property(e => e.ChangeDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.NewPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.OldPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.ChangedByEmployee).WithMany(p => p.PriceHistories)
                .HasForeignKey(d => d.ChangedByEmployeeId)
                .HasConstraintName("FK_PriceHistory_Employee");

            entity.HasOne(d => d.MenuItem).WithMany(p => p.PriceHistories)
                .HasForeignKey(d => d.MenuItemId)
                .HasConstraintName("FK_PriceHistory_MenuItem");
        });

        modelBuilder.Entity<Promotion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Promotio__3214EC07577996C2");

            entity.ToTable("Promotion");

            entity.HasIndex(e => new { e.IsActive, e.StartDate, e.EndDate }, "IX_Promotion_Active");

            entity.HasIndex(e => e.Code, "IX_Promotion_Code");

            entity.HasIndex(e => e.Code, "UQ__Promotio__A25C5AA7147079E8").IsUnique();

            entity.Property(e => e.ApplicableDays).HasMaxLength(50);
            entity.Property(e => e.ApplicableIds).HasMaxLength(500);
            entity.Property(e => e.ApplicableTo)
                .HasMaxLength(50)
                .HasDefaultValue("All");
            entity.Property(e => e.Code)
                .IsRequired()
                .HasMaxLength(100);
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.DiscountType)
                .IsRequired()
                .HasMaxLength(50);
            entity.Property(e => e.DiscountValue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.EndDate).HasColumnType("datetime");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.MaxDiscountAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.MinOrderValue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);
            entity.Property(e => e.StartDate).HasColumnType("datetime");
            entity.Property(e => e.UpdatedDate).HasColumnType("datetime");
        });

        modelBuilder.Entity<PromotionUsage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Promotio__3214EC07F55F0220");

            entity.ToTable("PromotionUsage");

            entity.HasIndex(e => e.CustomerPhone, "IX_PromotionUsage_Customer");

            entity.HasIndex(e => e.PromotionId, "IX_PromotionUsage_Promotion");

            entity.Property(e => e.CustomerPhone).HasMaxLength(20);
            entity.Property(e => e.DiscountApplied).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.UsedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Bills).WithMany(p => p.PromotionUsages)
                .HasForeignKey(d => d.BillId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_PromotionUsage_Bill");

            entity.HasOne(d => d.Promotion).WithMany(p => p.PromotionUsages)
                .HasForeignKey(d => d.PromotionId)
                .HasConstraintName("FK_PromotionUsage_Promotion");
        });

        modelBuilder.Entity<QROrder>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__QROrder__3214EC07B921B194");

            entity.ToTable("QROrder");

            entity.HasIndex(e => e.QROrderCode, "IX_QROrder_Code");

            entity.HasIndex(e => e.SessionToken, "IX_QROrder_Session");

            entity.HasIndex(e => e.Status, "IX_QROrder_Status");

            entity.HasIndex(e => e.QROrderCode, "UQ__QROrder__CF35D309D92BF567").IsUnique();

            entity.Property(e => e.AppliedPromotionCode).HasMaxLength(50);
            entity.Property(e => e.CompletedTime).HasColumnType("datetime");
            entity.Property(e => e.ConfirmedTime).HasColumnType("datetime");
            entity.Property(e => e.CreatedTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.CustomerName).HasMaxLength(100);
            entity.Property(e => e.CustomerPhone).HasMaxLength(20);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.QROrderCode)
                .IsRequired()
                .HasMaxLength(50);
            entity.Property(e => e.SessionToken)
                .IsRequired()
                .HasMaxLength(100);
            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("Draft");
            entity.Property(e => e.SubmittedTime).HasColumnType("datetime");

            entity.HasOne(d => d.ConfirmedByEmployee).WithMany(p => p.QROrders)
                .HasForeignKey(d => d.ConfirmedByEmployeeId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_QROrder_Employee");

            entity.HasOne(d => d.LinkedOrder).WithMany(p => p.QROrders)
                .HasForeignKey(d => d.LinkedOrderId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_QROrder_Order");

            entity.HasOne(d => d.Table).WithMany(p => p.QROrders)
                .HasForeignKey(d => d.TableId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_QROrder_Table");
        });

        modelBuilder.Entity<QROrderDetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__QROrderD__3214EC072F22A703");

            entity.ToTable("QROrderDetail");

            entity.HasIndex(e => e.QROrderId, "IX_QROrderDetail_Order");

            entity.Property(e => e.AddedTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.ItemNotes).HasMaxLength(200);
            entity.Property(e => e.ItemStatus)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("Pending");
            entity.Property(e => e.Quantity).HasDefaultValue(1);
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.MenuItem).WithMany(p => p.QROrderDetails)
                .HasForeignKey(d => d.MenuItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_QROrderDetail_MenuItem");

            entity.HasOne(d => d.QROrder).WithMany(p => p.QROrderDetails)
                .HasForeignKey(d => d.QROrderId)
                .HasConstraintName("FK_QROrderDetail_QROrder");
        });

        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.ToTable("Reservation");

            entity.HasIndex(e => new { e.ReservationDate, e.ReservationTime }, "IX_Reservation_Date");

            entity.HasIndex(e => e.ReservationCode, "UQ_Reservation_Code").IsUnique();

            entity.Property(e => e.CancelReason).HasMaxLength(500);
            entity.Property(e => e.CancelledDate).HasColumnType("datetime");
            entity.Property(e => e.ConfirmedDate).HasColumnType("datetime");
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.CustomerEmail).HasMaxLength(100);
            entity.Property(e => e.CustomerName)
                .IsRequired()
                .HasMaxLength(100);
            entity.Property(e => e.CustomerPhone)
                .IsRequired()
                .HasMaxLength(20);
            entity.Property(e => e.DepositAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ReservationCode)
                .IsRequired()
                .HasMaxLength(20);
            entity.Property(e => e.SpecialRequests).HasMaxLength(500);
            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("Pending");
            entity.Property(e => e.TablePreference).HasMaxLength(50);

            entity.HasOne(d => d.Customer).WithMany(p => p.Reservations)
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("FK_Reservation_Customer");
        });

        modelBuilder.Entity<RestaurantTable>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Restaura__3214EC07F4EDF4FC");

            entity.ToTable("RestaurantTable");

            entity.Property(e => e.Capacity).HasDefaultValue(4);
            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("Available");
            entity.Property(e => e.TableNumber)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasOne(d => d.TableArea).WithMany(p => p.RestaurantTables)
                .HasForeignKey(d => d.TableAreaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RestaurantTable_TableArea");
        });

        modelBuilder.Entity<ReturnBill>(entity =>
        {
            entity.ToTable("ReturnBill");

            entity.HasIndex(e => e.OriginalBillID, "IX_ReturnBill_OriginalBillID");

            entity.HasIndex(e => e.ReturnDate, "IX_ReturnBill_ReturnDate");

            entity.Property(e => e.Reason)
                .IsRequired()
                .HasMaxLength(500);
            entity.Property(e => e.ReturnDate).HasColumnType("datetime");
            entity.Property(e => e.TotalRefundAmount).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Employee).WithMany(p => p.ReturnBills)
                .HasForeignKey(d => d.EmployeeID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ReturnBill_Employee");

            entity.HasOne(d => d.OriginalBill).WithMany(p => p.ReturnBills)
                .HasForeignKey(d => d.OriginalBillID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ReturnBill_Bill");
        });

        modelBuilder.Entity<ReturnBillDetail>(entity =>
        {
            entity.ToTable("ReturnBillDetail");

            entity.HasIndex(e => e.ReturnBillID, "IX_ReturnBillDetail_ReturnBillID");

            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.MenuItem).WithMany(p => p.ReturnBillDetails)
                .HasForeignKey(d => d.MenuItemID)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ReturnBillDetail_MenuItem");

            entity.HasOne(d => d.ReturnBills).WithMany(p => p.ReturnBillDetails)
                .HasForeignKey(d => d.ReturnBillID)
                .HasConstraintName("FK_ReturnBillDetail_ReturnBill");
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.ToTable("Review");

            entity.HasIndex(e => e.MenuItemId, "IX_Review_MenuItem");

            entity.Property(e => e.AdminReply).HasMaxLength(500);
            entity.Property(e => e.AdminReplyDate).HasColumnType("datetime");
            entity.Property(e => e.Comment).HasMaxLength(1000);
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.ImageUrls).HasMaxLength(500);
            entity.Property(e => e.IsApproved).HasDefaultValue(true);
            entity.Property(e => e.UpdatedDate).HasColumnType("datetime");

            entity.HasOne(d => d.Customer).WithMany(p => p.Reviews)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Review_Customer");

            entity.HasOne(d => d.MenuItem).WithMany(p => p.Reviews)
                .HasForeignKey(d => d.MenuItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Review_MenuItem");

            entity.HasOne(d => d.Orders).WithMany(p => p.Reviews)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK_Review_Order");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Role__3214EC07AA4F84B5");

            entity.ToTable("Role");

            entity.HasIndex(e => e.RoleName, "UQ__Role__8A2B6160FEFFB055").IsUnique();

            entity.Property(e => e.RoleName)
                .IsRequired()
                .HasMaxLength(100);
        });

        modelBuilder.Entity<ShiftSupportStaff>(entity =>
        {
            entity.ToTable("ShiftSupportStaff");

            entity.HasOne(d => d.CashierShift).WithMany(p => p.ShiftSupportStaffs)
                .HasForeignKey(d => d.CashierShiftId)
                .HasConstraintName("FK_ShiftSupportStaff_CashierShift");

            entity.HasOne(d => d.Employee).WithMany(p => p.ShiftSupportStaffs)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ShiftSupportStaff_Employee");
        });

        modelBuilder.Entity<Shipper>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Shipper__3214EC07464095AA");

            entity.ToTable("Shipper");

            entity.HasIndex(e => e.Phone, "UQ__Shipper__5C7E359EFA3AE031").IsUnique();

            entity.Property(e => e.AvatarUrl).HasMaxLength(200);
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.CurrentLatitude).HasColumnType("decimal(10, 8)");
            entity.Property(e => e.CurrentLongitude).HasColumnType("decimal(11, 8)");
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.FullName)
                .IsRequired()
                .HasMaxLength(100);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.LastLocationUpdate).HasColumnType("datetime");
            entity.Property(e => e.LicensePlate).HasMaxLength(20);
            entity.Property(e => e.PasswordHash).HasMaxLength(256);
            entity.Property(e => e.Phone)
                .IsRequired()
                .HasMaxLength(20);
            entity.Property(e => e.Rating)
                .HasDefaultValue(5.0m)
                .HasColumnType("decimal(3, 2)");
            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("Offline");
            entity.Property(e => e.TotalEarnings).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.VehicleType)
                .HasMaxLength(50)
                .HasDefaultValue("Xe máy");
        });

        modelBuilder.Entity<StockInbound>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__StockInb__3214EC07BEDDCE9F");

            entity.ToTable("StockInbound");

            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.InboundCode).HasMaxLength(50);
            entity.Property(e => e.InboundDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.TotalCost).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Employee).WithMany(p => p.StockInbounds)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockInbound_Employee");

            entity.HasOne(d => d.Supplier).WithMany(p => p.StockInbounds)
                .HasForeignKey(d => d.SupplierId)
                .HasConstraintName("FK_StockInbound_Supplier");
        });

        modelBuilder.Entity<StockInboundDetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__StockInb__3214EC072DEC3198");

            entity.ToTable("StockInboundDetail");

            entity.Property(e => e.BatchNumber).HasMaxLength(100);
            entity.Property(e => e.Quantity).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Ingredient).WithMany(p => p.StockInboundDetails)
                .HasForeignKey(d => d.IngredientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StockInboundDetail_Ingredient");

            entity.HasOne(d => d.StockInbounds).WithMany(p => p.StockInboundDetails)
                .HasForeignKey(d => d.StockInboundId)
                .HasConstraintName("FK_StockInboundDetail_StockInbound");
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Supplier__3214EC07741B95AC");

            entity.ToTable("Supplier");

            entity.Property(e => e.ContactPerson).HasMaxLength(255);
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(255);
            entity.Property(e => e.PhoneNumber).HasMaxLength(20);
        });

        modelBuilder.Entity<TableArea>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__TableAre__3214EC07E19D52FE");

            entity.ToTable("TableArea");

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(100);
        });

        modelBuilder.Entity<TableSession>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__TableSes__3214EC0796F7FA65");

            entity.ToTable("TableSession");

            entity.HasIndex(e => new { e.TableId, e.Status }, "IX_TableSession_Table");

            entity.HasIndex(e => e.SessionToken, "IX_TableSession_Token");

            entity.HasIndex(e => e.SessionToken, "UQ__TableSes__46BDD12484905384").IsUnique();

            entity.Property(e => e.EndTime).HasColumnType("datetime");
            entity.Property(e => e.SessionToken)
                .IsRequired()
                .HasMaxLength(100);
            entity.Property(e => e.StartTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("Active");

            entity.HasOne(d => d.Table).WithMany(p => p.TableSessions)
                .HasForeignKey(d => d.TableId)
                .HasConstraintName("FK_TableSession_Table");
        });

        modelBuilder.Entity<VW_AccountInfo>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("VW_AccountInfo");

            entity.Property(e => e.CreatedBy).HasMaxLength(50);
            entity.Property(e => e.CreatedDate).HasColumnType("datetime");
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.FullName)
                .IsRequired()
                .HasMaxLength(100);
            entity.Property(e => e.LastLoginDate).HasColumnType("datetime");
            entity.Property(e => e.PhoneNumber).HasMaxLength(15);
            entity.Property(e => e.RoleName)
                .IsRequired()
                .HasMaxLength(100);
            entity.Property(e => e.StatusText)
                .IsRequired()
                .HasMaxLength(9);
            entity.Property(e => e.Username)
                .IsRequired()
                .HasMaxLength(50);
        });

        modelBuilder.Entity<VW_AppSetting>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("VW_AppSettings");

            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.CreatedDate).HasColumnType("datetime");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.SettingKey)
                .IsRequired()
                .HasMaxLength(100);
            entity.Property(e => e.SettingValue)
                .IsRequired()
                .HasMaxLength(1000);
            entity.Property(e => e.UpdatedBy).HasMaxLength(100);
            entity.Property(e => e.UpdatedDate).HasColumnType("datetime");
        });

        modelBuilder.Entity<Voucher>(entity =>
        {
            entity.ToTable("Voucher");

            entity.HasIndex(e => e.Code, "UQ_Voucher_Code").IsUnique();

            entity.Property(e => e.ApplicableItemIds).HasMaxLength(500);
            entity.Property(e => e.ApplicableTo)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("All");
            entity.Property(e => e.Code)
                .IsRequired()
                .HasMaxLength(50);
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.DiscountType)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("Percentage");
            entity.Property(e => e.DiscountValue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.EndDate).HasColumnType("datetime");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.MaxDiscountAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.MinOrderAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.PerCustomerLimit).HasDefaultValue(1);
            entity.Property(e => e.StartDate).HasColumnType("datetime");
        });

        modelBuilder.Entity<VoucherUsage>(entity =>
        {
            entity.ToTable("VoucherUsage");

            entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.UsedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Customer).WithMany(p => p.VoucherUsages)
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("FK_VoucherUsage_Customer");

            entity.HasOne(d => d.Orders).WithMany(p => p.VoucherUsages)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VoucherUsage_Order");

            entity.HasOne(d => d.Voucher).WithMany(p => p.VoucherUsages)
                .HasForeignKey(d => d.VoucherId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_VoucherUsage_Voucher");
        });

        modelBuilder.Entity<Wishlist>(entity =>
        {
            entity.ToTable("Wishlist");

            entity.HasIndex(e => new { e.CustomerId, e.MenuItemId }, "UQ_Wishlist_CustomerItem").IsUnique();

            entity.Property(e => e.AddedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Customer).WithMany(p => p.Wishlists)
                .HasForeignKey(d => d.CustomerId)
                .HasConstraintName("FK_Wishlist_Customer");

            entity.HasOne(d => d.MenuItem).WithMany(p => p.Wishlists)
                .HasForeignKey(d => d.MenuItemId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Wishlist_MenuItem");
        });

        modelBuilder.Entity<WorkShift>(entity =>
        {
            entity.ToTable("WorkShift");

            entity.Property(e => e.Description).HasMaxLength(200);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.ShiftName)
                .IsRequired()
                .HasMaxLength(50);
            entity.Property(e => e.WorkHours).HasColumnType("decimal(5, 2)");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
