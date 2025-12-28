using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NhaHangLDP.Data.Entities;

namespace NhaHangLDP.Data
{
    /// <summary>
    /// Adapter class để tương thích với code cũ sử dụng EF6 naming convention.
    /// Dần dần migrate sang NhaHangLDPContext trực tiếp với DI.
    /// </summary>
    public class NhaHangLDPEntities : NhaHangLDPContext
    {
        private static DbContextOptions<NhaHangLDPContext> _options;

        static NhaHangLDPEntities()
        {
            // Đọc connection string từ appsettings.json
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? "Data Source=LAPTOP-EHUTLUMK\\SQLEXPRESS;Initial Catalog=NhaHangLDP;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

            var optionsBuilder = new DbContextOptionsBuilder<NhaHangLDPContext>();
            optionsBuilder.UseSqlServer(connectionString);
            _options = optionsBuilder.Options;
        }

        public NhaHangLDPEntities() : base(_options)
        {
        }

        public NhaHangLDPEntities(DbContextOptions<NhaHangLDPContext> options) : base(options)
        {
        }

        // Aliases for backward compatibility (EF6 used singular names)
        public DbSet<Account> Account => Accounts;
        public DbSet<AppSetting> AppSetting => AppSettings;
        public DbSet<Attendance> Attendance => Attendances;
        public DbSet<Bill> Bill => Bills;
        public DbSet<Booking> Booking => Bookings;
        public DbSet<Cart> Cart => Carts;
        public DbSet<CartItem> CartItem => CartItems;
        public DbSet<CashierShift> CashierShift => CashierShifts;
        public DbSet<Customer> Customer => Customers;
        public DbSet<CustomerAddress> CustomerAddress => CustomerAddresses;
        public DbSet<CustomerNotification> CustomerNotification => CustomerNotifications;
        public DbSet<CustomerOrder> CustomerOrder => CustomerOrders;
        public DbSet<CustomerOrderDetail> CustomerOrderDetail => CustomerOrderDetails;
        public DbSet<DamagedStock> DamagedStock => DamagedStocks;
        public DbSet<DeliveryAssignment> DeliveryAssignment => DeliveryAssignments;
        public new DbSet<DeliverySetting> DeliverySettings => base.DeliverySettings;
        public DbSet<DeliveryZone> DeliveryZone => DeliveryZones;
        public DbSet<EmailConfig> EmailConfig => EmailConfigs;
        public DbSet<EmailLog> EmailLog => EmailLogs;
        public DbSet<EmailTemplate> EmailTemplate => EmailTemplates;
        public DbSet<Employee> Employee => Employees;
        public DbSet<EmployeeContract> EmployeeContract => EmployeeContracts;
        public DbSet<EmployeeSchedule> EmployeeSchedule => EmployeeSchedules;
        public DbSet<Ingredient> Ingredient => Ingredients;
        public DbSet<KitchenOrderItem> KitchenOrderItem => KitchenOrderItems;
        public DbSet<KitchenOrderTicket> KitchenOrderTicket => KitchenOrderTickets;
        public DbSet<KitchenStation> KitchenStation => KitchenStations;
        public DbSet<LeaveRequest> LeaveRequest => LeaveRequests;
        public DbSet<MenuCombo> MenuCombo => MenuCombos;
        public DbSet<MenuComboItem> MenuComboItem => MenuComboItems;
        public DbSet<MenuItem> MenuItem => MenuItems;
        public DbSet<MenuItemIngredient> MenuItemIngredient => MenuItemIngredients;
        public DbSet<Notification> Notification => Notifications;
        public DbSet<Order> Order => Orders;
        public DbSet<OrderDetail> OrderDetail => OrderDetails;
        public DbSet<Payroll> Payroll => Payrolls;
        public DbSet<PerformanceReview> PerformanceReview => PerformanceReviews;
        public DbSet<PriceHistory> PriceHistory => PriceHistories;
        public DbSet<Promotion> Promotion => Promotions;
        public DbSet<PromotionUsage> PromotionUsage => PromotionUsages;
        public DbSet<Qrorder> QROrder => Qrorders;
        public DbSet<QrorderDetail> QROrderDetail => QrorderDetails;
        public DbSet<Qrorder> Qrorder => Qrorders;
        public DbSet<QrorderDetail> QrorderDetail => QrorderDetails;
        public DbSet<Reservation> Reservation => Reservations;
        public DbSet<RestaurantTable> RestaurantTable => RestaurantTables;
        public DbSet<ReturnBill> ReturnBill => ReturnBills;
        public DbSet<ReturnBillDetail> ReturnBillDetail => ReturnBillDetails;
        public DbSet<Review> Review => Reviews;
        public DbSet<Role> Role => Roles;
        public DbSet<ShiftSupportStaff> ShiftSupportStaff => ShiftSupportStaffs;
        public DbSet<Shipper> Shipper => Shippers;
        public DbSet<StockInbound> StockInbound => StockInbounds;
        public DbSet<StockInboundDetail> StockInboundDetail => StockInboundDetails;
        public DbSet<Supplier> Supplier => Suppliers;
        public DbSet<TableArea> TableArea => TableAreas;
        public DbSet<TableSession> TableSession => TableSessions;
        public DbSet<Voucher> Voucher => Vouchers;
        public DbSet<VoucherUsage> VoucherUsage => VoucherUsages;
        public DbSet<Wishlist> Wishlist => Wishlists;
        public DbSet<WorkShift> WorkShift => WorkShifts;
    }
}
