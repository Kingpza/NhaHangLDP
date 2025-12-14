using System;
using System.Collections.Generic;

namespace NhaHangLDP.Models
{
    public class ShiftRevenueReportViewModel
    {
        public List<string> SupportStaffNames { get; set; }
        public string ShiftId { get; set; }
        public string CashierName { get; set; }
        public DateTime ShiftStartTime { get; set; }
        public DateTime ShiftEndTime { get; set; }
        public decimal OpeningAmount { get; set; }
        public decimal TotalRevenue { get; set; }
        public int TotalOrders { get; set; }
        public int CompletedOrders { get; set; }
        public int CancelledOrders { get; set; }
        public decimal CashInHand { get; set; }
        public string RestaurantName { get; set; }
        public string RestaurantAddress { get; set; }
        public string RestaurantPhone { get; set; }
        public string RestaurantTaxCode { get; set; }
        public List<PaymentMethodSummary> PaymentMethods { get; set; }
        public List<OrderSummary> TopOrders { get; set; }

        public ShiftRevenueReportViewModel()
        {
            PaymentMethods = new List<PaymentMethodSummary>();
            TopOrders = new List<OrderSummary>();
        }
    }

    public class PaymentMethodSummary
    {
        public string MethodName { get; set; }
        public int OrderCount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal Percentage { get; set; }
    }

    public class OrderSummary
    {
        public string OrderId { get; set; }
        public string TableName { get; set; }
        public DateTime OrderTime { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentMethod { get; set; }
    }
}