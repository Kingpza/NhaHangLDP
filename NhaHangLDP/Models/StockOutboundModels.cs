using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NhaHangLDP.Models
{
    // Additional ViewModels for Stock Operations
    public class StockInboundListViewModel
    {
        public List<StockInboundListItem> InboundList { get; set; }
        public string SearchTerm { get; set; }
        public string StatusFilter { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        public StockInboundListViewModel()
        {
            InboundList = new List<StockInboundListItem>();
        }
    }

    public class StockInboundListItem
    {
        public int Id { get; set; }
        public string InboundCode { get; set; }
        public DateTime InboundDate { get; set; }
        public string EmployeeName { get; set; }
        public string SupplierName { get; set; }
        public decimal TotalCost { get; set; }
        public string Status { get; set; }
        public int ItemCount { get; set; }
    }

    public class StockOutboundListViewModel
    {
        public List<StockOutboundListItem> OutboundList { get; set; }
        public string SearchTerm { get; set; }
        public string StatusFilter { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        public StockOutboundListViewModel()
        {
            OutboundList = new List<StockOutboundListItem>();
        }
    }

    public class StockOutboundListItem
    {
        public int Id { get; set; }
        public string OutboundCode { get; set; }
        public DateTime OutboundDate { get; set; }
        public string EmployeeName { get; set; }
        public string Purpose { get; set; }
        public decimal TotalCost { get; set; }
        public string Status { get; set; }
        public int ItemCount { get; set; }
    }
}