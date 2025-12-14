using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NhaHangLDP.Models
{
    public class ReturnManagementViewModel
    {
        [Display(Name = "Mã hóa đơn")]
        [Required(ErrorMessage = "Vui lòng nhập mã hóa đơn")]
        [Range(1, int.MaxValue, ErrorMessage = "Mã hóa đơn phải là số nguyên dương.")]
        public int BillID { get; set; }

        [Display(Name = "Lý do trả hàng")]
        [Required(ErrorMessage = "Vui lòng nhập lý do trả hàng")]
        [StringLength(500, ErrorMessage = "Lý do không được vượt quá 500 ký tự")]
        public string Reason { get; set; }

        [Display(Name = "Nhân viên xử lý")]
        [Required(ErrorMessage = "Vui lòng chọn nhân viên xử lý")]
        // Thêm Range check nếu EmployeeID không phải là nullable
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn nhân viên xử lý.")]
        public int EmployeeID { get; set; }

        // Thông tin hóa đơn gốc (Chỉ dùng cho việc hiển thị/xác thực lại)
        public Bill OriginalBill { get; set; }
        public List<OrderDetailViewModel> OriginalOrderDetails { get; set; }

        // Chi tiết trả hàng
        public List<ReturnItemViewModel> ReturnItems { get; set; }

        // Tổng tiền hoàn trả
        public decimal TotalRefundAmount { get; set; }

        public ReturnManagementViewModel()
        {
            OriginalOrderDetails = new List<OrderDetailViewModel>();
            ReturnItems = new List<ReturnItemViewModel>();
        }
    }

    public class OrderDetailViewModel
    {
        public int OrderDetailID { get; set; }
        public int MenuItemID { get; set; }
        public string MenuItemName { get; set; }
        public int Quantity { get; set; }
        public decimal PriceAtTime { get; set; }
        public decimal TotalAmount { get; set; }
        public string Notes { get; set; }
    }

    public class ReturnItemViewModel
    {
        // THUỘC TÍNH MỚI: Dùng cho logic cập nhật kho, đặc biệt khi ghi log hàng hỏng
        public int BillID { get; set; }

        public int MenuItemID { get; set; }
        public string MenuItemName { get; set; }
        public int MaxQuantity { get; set; } // Số lượng tối đa có thể trả

        [Display(Name = "Số lượng trả")]
        [Required(ErrorMessage = "Vui lòng nhập số lượng trả")]
        // ĐIỀU CHỈNH: Cho phép ReturnQuantity = 0. Validation "ít nhất một món" được xử lý ở Controller/JS.
        [Range(0, int.MaxValue, ErrorMessage = "Số lượng trả không hợp lệ.")]
        public int ReturnQuantity { get; set; }

        [Display(Name = "Đơn giá")]
        public decimal UnitPrice { get; set; }

        [Display(Name = "Hàng bị hỏng")]
        public bool IsDamaged { get; set; }

        public decimal TotalAmount => ReturnQuantity * UnitPrice;
    }

    public class BillSearchResultViewModel
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        // Lưu ý: Thuộc tính Bill này phải là một đối tượng DTO đơn giản 
        // hoặc đối tượng ẩn danh để tránh lỗi serialization vòng lặp trong AJAX.
        public object Bill { get; set; }
        public List<OrderDetailViewModel> OrderDetails { get; set; }

        public BillSearchResultViewModel()
        {
            OrderDetails = new List<OrderDetailViewModel>();
        }
    }
}