// Navigation property aliases cho backward compatibility
// EF6 sử dụng tên số ít, EF Core scaffold sử dụng tên số nhiều
// File này thêm các computed properties để code cũ vẫn hoạt động
// Sử dụng [NotMapped] để EF Core bỏ qua các alias properties này

using System.ComponentModel.DataAnnotations.Schema;

namespace NhaHangLDP.Data.Entities
{
    // CashierShift aliases - EF Core dùng 'Cashier', code cũ dùng 'Employee'
    public partial class CashierShift
    {
        [NotMapped]
        public virtual Employee Employee => Cashier;
        [NotMapped]
        public virtual ICollection<Order> Order => Orders;
        [NotMapped]
        public virtual ICollection<ShiftSupportStaff> ShiftSupportStaff => ShiftSupportStaffs;
    }

    // Order aliases - EF Core dùng tên số nhiều
    public partial class Order
    {
        [NotMapped]
        public virtual ICollection<OrderDetail> OrderDetail => OrderDetails;
        [NotMapped]
        public virtual ICollection<Bill> Bill => Bills;
        [NotMapped]
        public virtual ICollection<KitchenOrderTicket> KitchenOrderTicket => KitchenOrderTickets;
        [NotMapped]
        public virtual RestaurantTable RestaurantTable => Table;
        [NotMapped]
        public virtual Employee Employee => Waiter;
    }

    // Bill aliases
    public partial class Bill
    {
        [NotMapped]
        public virtual ICollection<ReturnBill> ReturnBill => ReturnBills;
        [NotMapped]
        public virtual ICollection<PromotionUsage> PromotionUsage => PromotionUsages;
        [NotMapped]
        public virtual Employee Employee => Cashier;
    }

    // RestaurantTable aliases
    public partial class RestaurantTable
    {
        [NotMapped]
        public virtual ICollection<Order> Order => Orders;
        [NotMapped]
        public virtual ICollection<Booking> Booking => Bookings;
        [NotMapped]
        public virtual ICollection<TableSession> TableSession => TableSessions;
    }

    // Customer aliases
    public partial class Customer
    {
        [NotMapped]
        public virtual ICollection<CustomerOrder> CustomerOrder => CustomerOrders;
        [NotMapped]
        public virtual ICollection<CustomerAddress> CustomerAddress => CustomerAddresses;
        [NotMapped]
        public virtual ICollection<Wishlist> Wishlist => Wishlists;
        [NotMapped]
        public virtual ICollection<Review> Review => Reviews;
        [NotMapped]
        public virtual ICollection<Cart> Cart => Carts;
        [NotMapped]
        public virtual ICollection<VoucherUsage> VoucherUsage => VoucherUsages;
        [NotMapped]
        public virtual ICollection<Reservation> Reservation => Reservations;
    }

    // CustomerOrder aliases
    public partial class CustomerOrder
    {
        [NotMapped]
        public virtual ICollection<CustomerOrderDetail> CustomerOrderDetail => CustomerOrderDetails;
        [NotMapped]
        public virtual ICollection<DeliveryAssignment> DeliveryAssignment => DeliveryAssignments;
    }

    // MenuItem aliases
    public partial class MenuItem
    {
        [NotMapped]
        public virtual ICollection<OrderDetail> OrderDetail => OrderDetails;
        [NotMapped]
        public virtual ICollection<CartItem> CartItem => CartItems;
        [NotMapped]
        public virtual ICollection<MenuItemIngredient> MenuItemIngredient => MenuItemIngredients;
        [NotMapped]
        public virtual ICollection<PriceHistory> PriceHistory => PriceHistories;
        [NotMapped]
        public virtual ICollection<Review> Review => Reviews;
        [NotMapped]
        public virtual ICollection<MenuComboItem> MenuComboItem => MenuComboItems;
    }

    // Ingredient aliases
    public partial class Ingredient
    {
        [NotMapped]
        public virtual ICollection<MenuItemIngredient> MenuItemIngredient => MenuItemIngredients;
        [NotMapped]
        public virtual ICollection<StockInboundDetail> StockInboundDetail => StockInboundDetails;
        [NotMapped]
        public virtual ICollection<DamagedStock> DamagedStock => DamagedStocks;
    }

    // StockInbound aliases
    public partial class StockInbound
    {
        [NotMapped]
        public virtual ICollection<StockInboundDetail> StockInboundDetail => StockInboundDetails;
    }

    // Supplier aliases
    public partial class Supplier
    {
        [NotMapped]
        public virtual ICollection<StockInbound> StockInbound => StockInbounds;
    }

    // Promotion aliases
    public partial class Promotion
    {
        [NotMapped]
        public virtual ICollection<PromotionUsage> PromotionUsage => PromotionUsages;
    }

    // Voucher aliases
    public partial class Voucher
    {
        [NotMapped]
        public virtual ICollection<VoucherUsage> VoucherUsage => VoucherUsages;
    }

    // DeliveryAssignment aliases
    public partial class DeliveryAssignment
    {
        [NotMapped]
        public virtual CustomerOrder CustomerOrder => Order;
    }

    // Shipper aliases
    public partial class Shipper
    {
        [NotMapped]
        public virtual ICollection<DeliveryAssignment> DeliveryAssignment => DeliveryAssignments;
    }

    // TableArea aliases
    public partial class TableArea
    {
        [NotMapped]
        public virtual ICollection<RestaurantTable> RestaurantTable => RestaurantTables;
    }

    // Role aliases
    public partial class Role
    {
        [NotMapped]
        public virtual ICollection<Account> Account => Accounts;
        [NotMapped]
        public virtual ICollection<Employee> Employee => Employees;
    }

    // Employee aliases
    public partial class Employee
    {
        [NotMapped]
        public virtual ICollection<Order> Order => Orders;
        [NotMapped]
        public virtual ICollection<Attendance> Attendance => Attendances;
        [NotMapped]
        public virtual ICollection<EmployeeSchedule> EmployeeSchedule => EmployeeSchedules;
        [NotMapped]
        public virtual ICollection<EmployeeContract> EmployeeContract => EmployeeContracts;
        [NotMapped]
        public virtual ICollection<LeaveRequest> LeaveRequest => LeaveRequestEmployees;
        [NotMapped]
        public virtual ICollection<PerformanceReview> PerformanceReview => PerformanceReviewEmployees;
        [NotMapped]
        public virtual ICollection<Payroll> Payroll => Payrolls;
        [NotMapped]
        public virtual ICollection<CashierShift> CashierShift => CashierShifts;
    }

    // WorkShift aliases
    public partial class WorkShift
    {
        [NotMapped]
        public virtual ICollection<EmployeeSchedule> EmployeeSchedule => EmployeeSchedules;
    }

    // MenuCombo aliases
    public partial class MenuCombo
    {
        [NotMapped]
        public virtual ICollection<MenuComboItem> MenuComboItem => MenuComboItems;
    }

    // Qrorder aliases
    public partial class Qrorder
    {
        [NotMapped]
        public virtual ICollection<QrorderDetail> QROrderDetail => QrorderDetails;
        [NotMapped]
        public string QROrderCode => QrorderCode;
        [NotMapped]
        public virtual RestaurantTable RestaurantTable => Table;
    }

    // QrorderDetail aliases
    public partial class QrorderDetail
    {
        [NotMapped]
        public int QROrderId => QrorderId;
        [NotMapped]
        public virtual Qrorder QROrder => Qrorder;
    }

    // ReturnBill aliases
    public partial class ReturnBill
    {
        [NotMapped]
        public virtual ICollection<ReturnBillDetail> ReturnBillDetail => ReturnBillDetails;
        [NotMapped]
        public int ReturnBillID => ReturnBillId;
        [NotMapped]
        public int OriginalBillID => OriginalBillId;
        [NotMapped]
        public virtual Bill Bill => OriginalBill;
    }

    // ReturnBillDetail aliases
    public partial class ReturnBillDetail
    {
        [NotMapped]
        public int ReturnBillDetailID => ReturnBillDetailId;
    }

    // KitchenOrderTicket aliases
    public partial class KitchenOrderTicket
    {
        [NotMapped]
        public virtual ICollection<KitchenOrderItem> KitchenOrderItem => KitchenOrderItems;
        [NotMapped]
        public virtual RestaurantTable RestaurantTable => Table;
        [NotMapped]
        public virtual Employee Employee => AssignedChef;
    }

    // DamagedStock aliases
    public partial class DamagedStock
    {
        [NotMapped]
        public virtual Employee Employee => ReportedByEmployee;
    }

    // Cart aliases
    public partial class Cart
    {
        [NotMapped]
        public virtual ICollection<CartItem> CartItem => CartItems;
    }
}
