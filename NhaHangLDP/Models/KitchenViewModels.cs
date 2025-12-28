using NhaHangLDP.Data.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace NhaHangLDP.Models
{
    /// <summary>
    /// ViewModel cho Kitchen Display Screen
    /// </summary>
    public class KitchenDisplayViewModel
    {
        public List<KitchenTicketViewModel> Tickets { get; set; }
        public List<KitchenStationViewModel> Stations { get; set; }
        public KitchenStatsViewModel Stats { get; set; }
        public string CurrentStation { get; set; }
        public DateTime LastUpdated { get; set; }

        public KitchenDisplayViewModel()
        {
            Tickets = new List<KitchenTicketViewModel>();
            Stations = new List<KitchenStationViewModel>();
            Stats = new KitchenStatsViewModel();
            LastUpdated = DateTime.Now;
        }
    }

    /// <summary>
    /// ViewModel cho từng ticket trong bếp
    /// </summary>
    public class KitchenTicketViewModel
    {
        public int Id { get; set; }
        public string TicketCode { get; set; }
        public int OrderId { get; set; }
        public string OrderCode { get; set; }
        public int TableId { get; set; }
        public string TableNumber { get; set; }
        public string Status { get; set; }
        public int Priority { get; set; }
        public string SpecialNotes { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime? StartedTime { get; set; }
        public DateTime? CompletedTime { get; set; }
        public int EstimatedMinutes { get; set; }
        public int? AssignedChefId { get; set; }
        public string AssignedChefName { get; set; }
        public string KitchenStation { get; set; }
        public bool IsPrinted { get; set; }
        public int PrintCount { get; set; }
        public List<KitchenItemViewModel> Items { get; set; }

        // Computed properties
        public int WaitingMinutes
        {
            get
            {
                var startTime = StartedTime ?? CreatedTime;
                return (int)(DateTime.Now - startTime).TotalMinutes;
            }
        }

        public bool IsOverdue => WaitingMinutes > EstimatedMinutes && Status != "Completed";

        public string StatusText
        {
            get
            {
                switch (Status?.ToLower())
                {
                    case "pending": return "Chờ xử lý";
                    case "preparing": return "Đang chuẩn bị";
                    case "ready": return "Sẵn sàng";
                    case "completed": return "Hoàn thành";
                    case "cancelled": return "Đã hủy";
                    default: return Status;
                }
            }
        }

        public string StatusClass
        {
            get
            {
                if (IsOverdue && Status != "Completed" && Status != "Cancelled")
                    return "danger";
                    
                switch (Status?.ToLower())
                {
                    case "pending": return "warning";
                    case "preparing": return "info";
                    case "ready": return "success";
                    case "completed": return "secondary";
                    case "cancelled": return "dark";
                    default: return "secondary";
                }
            }
        }

        public string PriorityText
        {
            get
            {
                switch (Priority)
                {
                    case 1: return "Thấp";
                    case 2: return "Bình thường";
                    case 3: return "Cao";
                    case 4: return "Rất cao";
                    case 5: return "Khẩn cấp";
                    default: return "Bình thường";
                }
            }
        }

        public string PriorityClass
        {
            get
            {
                switch (Priority)
                {
                    case 1: return "secondary";
                    case 2: return "primary";
                    case 3: return "warning";
                    case 4: return "danger";
                    case 5: return "danger pulse";
                    default: return "primary";
                }
            }
        }

        public int CompletedItemCount => Items?.Count(i => i.Status == "Completed") ?? 0;
        public int TotalItemCount => Items?.Count ?? 0;
        public int ProgressPercent => TotalItemCount > 0 ? (CompletedItemCount * 100 / TotalItemCount) : 0;

        public KitchenTicketViewModel()
        {
            Items = new List<KitchenItemViewModel>();
            Priority = 2;
            Status = "Pending";
        }
    }

    /// <summary>
    /// ViewModel cho món ăn trong ticket bếp
    /// </summary>
    public class KitchenItemViewModel
    {
        public int Id { get; set; }
        public int KitchenOrderTicketId { get; set; }
        public int? OrderDetailId { get; set; }
        public int MenuItemId { get; set; }
        public string ItemName { get; set; }
        public string ItemImage { get; set; }
        public int Quantity { get; set; }
        public int CompletedQuantity { get; set; }
        public string Status { get; set; }
        public string CustomerNotes { get; set; }
        public string KitchenNotes { get; set; }
        public DateTime? StartedTime { get; set; }
        public DateTime? CompletedTime { get; set; }
        public string Station { get; set; }

        public int RemainingQuantity => Quantity - CompletedQuantity;
        public bool IsCompleted => CompletedQuantity >= Quantity;

        public string StatusText
        {
            get
            {
                if (IsCompleted) return "Xong";
                switch (Status?.ToLower())
                {
                    case "pending": return "Chờ";
                    case "preparing": return "Đang làm";
                    case "completed": return "Xong";
                    default: return Status;
                }
            }
        }

        public string StatusClass
        {
            get
            {
                if (IsCompleted) return "success";
                switch (Status?.ToLower())
                {
                    case "pending": return "warning";
                    case "preparing": return "info";
                    default: return "secondary";
                }
            }
        }
    }

    /// <summary>
    /// ViewModel cho station trong bếp
    /// </summary>
    public class KitchenStationViewModel
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string HandledCategories { get; set; }
        public string DisplayColor { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
        public int PendingTicketCount { get; set; }
        public int PreparingTicketCount { get; set; }
        public int TotalItemCount { get; set; }

        public List<string> CategoryList => HandledCategories?.Split(',').Select(c => c.Trim()).ToList() ?? new List<string>();
    }

    /// <summary>
    /// Thống kê bếp
    /// </summary>
    public class KitchenStatsViewModel
    {
        public int TotalPendingTickets { get; set; }
        public int TotalPreparingTickets { get; set; }
        public int TotalReadyTickets { get; set; }
        public int TotalCompletedToday { get; set; }
        public int OverdueTickets { get; set; }
        public double AveragePreparationTime { get; set; }
        public int TotalItemsToday { get; set; }
    }

    /// <summary>
    /// DTO để cập nhật trạng thái ticket
    /// </summary>
    public class UpdateTicketStatusDto
    {
        [Required]
        public int TicketId { get; set; }
        
        [Required]
        public string NewStatus { get; set; }
        
        public int? ChefId { get; set; }
        public string Notes { get; set; }
    }

    /// <summary>
    /// DTO để cập nhật trạng thái món trong ticket
    /// </summary>
    public class UpdateKitchenItemDto
    {
        [Required]
        public int ItemId { get; set; }
        
        [Required]
        public string NewStatus { get; set; }
        
        public int? CompletedQuantity { get; set; }
        public string KitchenNotes { get; set; }
    }

    /// <summary>
    /// DTO để tạo ticket mới từ order
    /// </summary>
    public class CreateKitchenTicketDto
    {
        [Required]
        public int OrderId { get; set; }
        
        public int Priority { get; set; }
        public string SpecialNotes { get; set; }
        public string Station { get; set; }
        public int? AssignedChefId { get; set; }
    }

    /// <summary>
    /// ViewModel cho màn hình quản lý station
    /// </summary>
    public class StationManagementViewModel
    {
        public List<KitchenStationViewModel> Stations { get; set; }
        public List<string> AvailableCategories { get; set; }
        public List<ChefViewModel> AvailableChefs { get; set; }

        public StationManagementViewModel()
        {
            Stations = new List<KitchenStationViewModel>();
            AvailableCategories = new List<string>();
            AvailableChefs = new List<ChefViewModel>();
        }
    }

    /// <summary>
    /// ViewModel cho đầu bếp
    /// </summary>
    public class ChefViewModel
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Station { get; set; }
        public int ActiveTicketCount { get; set; }
        public bool IsAvailable { get; set; }
    }

    /// <summary>
    /// ViewModel cho form tạo/sửa station
    /// </summary>
    public class StationFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã station")]
        [StringLength(20)]
        [Display(Name = "Mã Station")]
        public string Code { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên station")]
        [StringLength(50)]
        [Display(Name = "Tên Station")]
        public string Name { get; set; }

        [Display(Name = "Mô tả")]
        public string Description { get; set; }

        [Display(Name = "Danh mục xử lý")]
        public string HandledCategories { get; set; }

        [Display(Name = "Màu hiển thị")]
        public string DisplayColor { get; set; }

        [Display(Name = "Thứ tự hiển thị")]
        public int DisplayOrder { get; set; }

        [Display(Name = "Trạng thái")]
        public bool IsActive { get; set; }

        public List<string> AvailableCategories { get; set; }
        public bool IsEdit { get; set; }

        public StationFormViewModel()
        {
            IsActive = true;
            DisplayColor = "#3b82f6";
            AvailableCategories = new List<string>();
        }
    }

    /// <summary>
    /// Response cho real-time updates
    /// </summary>
    public class KitchenUpdateResponse
    {
        public string UpdateType { get; set; } // NewTicket, StatusChanged, ItemCompleted, TicketCompleted
        public int TicketId { get; set; }
        public int? ItemId { get; set; }
        public string NewStatus { get; set; }
        public DateTime UpdateTime { get; set; }
        public KitchenTicketViewModel Ticket { get; set; }
        public KitchenStatsViewModel Stats { get; set; }
    }
}
