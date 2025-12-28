// Navigation property aliases cho backward compatibility
// EF6 sử dụng tên số ít, EF Core scaffold sử dụng tên số nhiều

namespace NhaHangLDP.Data.Entities
{
    // RestaurantTable aliases
    public partial class RestaurantTable
    {
        public virtual ICollection<Order> Order => Orders;
        public virtual ICollection<Booking> Booking => Bookings;
        public virtual ICollection<TableSession> TableSession => TableSessions;
    }

    // Order aliases
    public partial class Order
    {
        public virtual ICollection<OrderDetail> OrderDetail => OrderDetails;
        public virtual ICollection<Bill> Bill => Bills;
        public virtual ICollection<KitchenOrderTicket> KitchenOrderTicket => KitchenOrderTickets;
        // Navigation property alias
        public virtual RestaurantTable RestaurantTable => Table;
        public virtual Employee Employee => Waiter;
    }

    // CashierShift aliases
    public partial class CashierShift
    {
        public virtual ICollection<Order> Order => Orders;
        public virtual ICollection<ShiftSupportStaff> ShiftSupportStaff => ShiftSupportStaffs;
        // Employee alias - EF Core uses 'Cashier' 
        public virtual Employee Employee => Cashier;
    }

    // Employee aliases
    public partial class Employee
    {
        public virtual ICollection<Order> Order => Orders;
        public virtual ICollection<Attendance> Attendance => Attendances;
        public virtual ICollection<EmployeeSchedule> EmployeeSchedule => EmployeeSchedules;
        public virtual ICollection<EmployeeContract> EmployeeContract => EmployeeContracts;
        public virtual ICollection<LeaveRequest> LeaveRequest => LeaveRequestEmployees;
        public virtual ICollection<PerformanceReview> PerformanceReview => PerformanceReviewEmployees;
        public virtual ICollection<Payroll> Payroll => Payrolls;
        public virtual ICollection<CashierShift> CashierShift => CashierShifts;
    }

    // Bill aliases
    public partial class Bill
    {
        public virtual ICollection<ReturnBill> ReturnBill => ReturnBills;
        public virtual ICollection<PromotionUsage> PromotionUsage => PromotionUsages;
        // Navigation alias
        public virtual Employee Employee => Cashier;
    }

    // Customer aliases
    public partial class Customer
    {
        public virtual ICollection<CustomerOrder> CustomerOrder => CustomerOrders;
        public virtual ICollection<CustomerAddress> CustomerAddress => CustomerAddresses;
        public virtual ICollection<Wishlist> Wishlist => Wishlists;
        public virtual ICollection<Review> Review => Reviews;
        public virtual ICollection<Cart> Cart => Carts;
        public virtual ICollection<VoucherUsage> VoucherUsage => VoucherUsages;
        public virtual ICollection<Reservation> Reservation => Reservations;
    }

    // MenuItem aliases
    public partial class MenuItem
    {
        public virtual ICollection<OrderDetail> OrderDetail => OrderDetails;
        public virtual ICollection<CartItem> CartItem => CartItems;
        public virtual ICollection<MenuItemIngredient> MenuItemIngredient => MenuItemIngredients;
        public virtual ICollection<PriceHistory> PriceHistory => PriceHistories;
        public virtual ICollection<Review> Review => Reviews;
        public virtual ICollection<MenuComboItem> MenuComboItem => MenuComboItems;
    }

    // Ingredient aliases
    public partial class Ingredient
    {
        public virtual ICollection<MenuItemIngredient> MenuItemIngredient => MenuItemIngredients;
        public virtual ICollection<StockInboundDetail> StockInboundDetail => StockInboundDetails;
        public virtual ICollection<DamagedStock> DamagedStock => DamagedStocks;
    }

    // Supplier aliases
    public partial class Supplier
    {
        public virtual ICollection<StockInbound> StockInbound => StockInbounds;
    }

    // StockInbound aliases
    public partial class StockInbound
    {
        public virtual ICollection<StockInboundDetail> StockInboundDetail => StockInboundDetails;
    }

    // Promotion aliases
    public partial class Promotion
    {
        public virtual ICollection<PromotionUsage> PromotionUsage => PromotionUsages;
    }

    // Voucher aliases
    public partial class Voucher
    {
        public virtual ICollection<VoucherUsage> VoucherUsage => VoucherUsages;
    }

    // CustomerOrder aliases
    public partial class CustomerOrder
    {
        public virtual ICollection<CustomerOrderDetail> CustomerOrderDetail => CustomerOrderDetails;
        public virtual ICollection<DeliveryAssignment> DeliveryAssignment => DeliveryAssignments;
    }

    // DeliveryAssignment aliases
    public partial class DeliveryAssignment
    {
        // EF Core uses 'Order' for CustomerOrder navigation
        public virtual CustomerOrder CustomerOrder => Order;
    }

    // Shipper aliases
    public partial class Shipper
    {
        public virtual ICollection<DeliveryAssignment> DeliveryAssignment => DeliveryAssignments;
    }

    // TableArea aliases
    public partial class TableArea
    {
        public virtual ICollection<RestaurantTable> RestaurantTable => RestaurantTables;
    }

    // Role aliases
    public partial class Role
    {
        public virtual ICollection<Account> Account => Accounts;
        public virtual ICollection<Employee> Employee => Employees;
    }

    // WorkShift aliases
    public partial class WorkShift
    {
        public virtual ICollection<EmployeeSchedule> EmployeeSchedule => EmployeeSchedules;
    }

    // MenuCombo aliases
    public partial class MenuCombo
    {
        public virtual ICollection<MenuComboItem> MenuComboItem => MenuComboItems;
    }

    // QROrder aliases (Qrorder)
    public partial class Qrorder
    {
        public virtual ICollection<QrorderDetail> QROrderDetail => QrorderDetails;
        // Property aliases
        public string QROrderCode => QrorderCode;
        // Navigation alias
        public virtual RestaurantTable RestaurantTable => Table;
    }

    // QROrderDetail aliases (QrorderDetail)
    public partial class QrorderDetail
    {
        // Property aliases
        public int QROrderId => QrorderId;
        // Navigation alias
        public virtual Qrorder QROrder => Qrorder;
    }

    // ReturnBill aliases
    public partial class ReturnBill
    {
        public virtual ICollection<ReturnBillDetail> ReturnBillDetail => ReturnBillDetails;
        
        // Property aliases for backward compatibility
        public int ReturnBillID => ReturnBillId;
        public int OriginalBillID => OriginalBillId;
        public virtual Bill Bill => OriginalBill;
    }

    // ReturnBillDetail aliases
    public partial class ReturnBillDetail
    {
        // Property aliases
        public int ReturnBillDetailID => ReturnBillDetailId;
    }

    // KitchenOrderTicket aliases
    public partial class KitchenOrderTicket
    {
        public virtual ICollection<KitchenOrderItem> KitchenOrderItem => KitchenOrderItems;
        // Navigation alias
        public virtual RestaurantTable RestaurantTable => Table;
        public virtual Employee Employee => AssignedChef;
    }

    // DamagedStock aliases
    public partial class DamagedStock
    {
        // Navigation alias
        public virtual Employee Employee => ReportedByEmployee;
    }

    // Cart aliases
    public partial class Cart
    {
        public virtual ICollection<CartItem> CartItem => CartItems;
    }
}
