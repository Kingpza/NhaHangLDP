using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using NhaHangLDP.Models;
using NhaHangLDP.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace NhaHangLDP.Controllers
{
    /// <summary>
    /// Controller cho Kitchen Display System
    /// </summary>
    public class KitchenController : Controller
    {
        private NhaHangLDPEntities db = new NhaHangLDPEntities();

        #region Display Pages

        /// <summary>
        /// Màn hình chính Kitchen Display
        /// </summary>
        [CustomAuthorize("Admin", "Manager", "Kitchen")]
        public ActionResult Display(string station = "")
        {
            var viewModel = GetKitchenDisplayData(station);
            return View(viewModel);
        }

        /// <summary>
        /// Màn hình toàn màn hình cho TV
        /// </summary>
        [CustomAuthorize("Admin", "Manager", "Kitchen")]
        public ActionResult FullScreen(string station = "")
        {
            var viewModel = GetKitchenDisplayData(station);
            return View(viewModel);
        }

        /// <summary>
        /// Dashboard tổng quan bếp
        /// </summary>
        [CustomAuthorize("Admin", "Manager")]
        public ActionResult Dashboard()
        {
            var today = DateTime.Today;
            var viewModel = new KitchenDisplayViewModel
            {
                Stats = GetKitchenStats(),
                Stations = GetStationsWithStats(),
                LastUpdated = DateTime.Now
            };

            return View(viewModel);
        }

        #endregion

        #region Ticket Management

        /// <summary>
        /// Tạo ticket từ Order
        /// </summary>
        [HttpPost]
        [CustomAuthorize("Admin", "Manager", "Cashier")]
        public JsonResult CreateTicket(CreateKitchenTicketDto dto)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var order = db.Order
                        .Include(o => o.OrderDetail.Select(od => od.MenuItem))
                        .Include(o => o.RestaurantTable)
                        .FirstOrDefault(o => o.Id == dto.OrderId);

                    if (order == null)
                    {
                        return Json(new { success = false, message = "Không tìm thấy đơn hàng" });
                    }

                    // Kiểm tra đã có ticket chưa
                    if (db.KitchenOrderTicket.Any(t => t.OrderId == dto.OrderId && t.Status != "Completed" && t.Status != "Cancelled"))
                    {
                        return Json(new { success = false, message = "Đơn hàng đã có ticket trong bếp" });
                    }

                    // Tính thời gian ước tính
                    var maxPrepTime = order.OrderDetail.Max(od => od.MenuItem.PreparationTime);

                    var ticket = new KitchenOrderTicket
                    {
                        TicketCode = "KT" + DateTime.Now.ToString("yyyyMMddHHmmss") + new Random().Next(10, 99),
                        OrderId = dto.OrderId,
                        TableId = order.TableId,
                        Status = "Pending",
                        Priority = dto.Priority > 0 ? dto.Priority : 2,
                        SpecialNotes = dto.SpecialNotes,
                        CreatedTime = DateTime.Now,
                        EstimatedMinutes = maxPrepTime > 0 ? maxPrepTime : 15,
                        KitchenStation = dto.Station,
                        AssignedChefId = dto.AssignedChefId,
                        IsPrinted = false,
                        PrintCount = 0
                    };

                    db.KitchenOrderTicket.Add(ticket);
                    db.SaveChanges();

                    // Tạo kitchen items
                    foreach (var detail in order.OrderDetail)
                    {
                        var item = new KitchenOrderItem
                        {
                            KitchenOrderTicketId = ticket.Id,
                            OrderDetailId = detail.Id,
                            MenuItemId = detail.MenuItemId,
                            ItemName = detail.MenuItem.Name,
                            Quantity = detail.Quantity,
                            CompletedQuantity = 0,
                            Status = "Pending",
                            CustomerNotes = detail.Notes,
                            Station = GetStationForCategory(detail.MenuItem.Category)
                        };
                        db.KitchenOrderItem.Add(item);
                    }

                    // Cập nhật Order status
                    order.Status = "Preparing";

                    db.SaveChanges();
                    transaction.Commit();

                    return Json(new
                    {
                        success = true,
                        message = "Đã gửi đơn xuống bếp!",
                        ticketId = ticket.Id,
                        ticketCode = ticket.TicketCode
                    });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
                }
            }
        }

        /// <summary>
        /// Bắt đầu chuẩn bị ticket
        /// </summary>
        [HttpPost]
        [CustomAuthorize("Admin", "Manager", "Kitchen")]
        public JsonResult StartTicket(int ticketId)
        {
            try
            {
                var ticket = db.KitchenOrderTicket.Find(ticketId);
                if (ticket == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy ticket" });
                }

                if (ticket.Status != "Pending")
                {
                    return Json(new { success = false, message = "Ticket không ở trạng thái chờ" });
                }

                ticket.Status = "Preparing";
                ticket.StartedTime = DateTime.Now;
                ticket.AssignedChefId = ticket.AssignedChefId ?? GetCurrentEmployeeId();

                // Cập nhật items
                foreach (var item in db.KitchenOrderItem.Where(i => i.KitchenOrderTicketId == ticketId))
                {
                    if (item.Status == "Pending")
                    {
                        item.Status = "Preparing";
                        item.StartedTime = DateTime.Now;
                    }
                }

                db.SaveChanges();

                return Json(new
                {
                    success = true,
                    message = "Đã bắt đầu chuẩn bị",
                    ticketId = ticket.Id
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        /// <summary>
        /// Hoàn thành món trong ticket
        /// </summary>
        [HttpPost]
        [CustomAuthorize("Admin", "Manager", "Kitchen")]
        public JsonResult CompleteItem(UpdateKitchenItemDto dto)
        {
            try
            {
                var item = db.KitchenOrderItem
                    .Include(i => i.KitchenOrderTicket)
                    .FirstOrDefault(i => i.Id == dto.ItemId);

                if (item == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy món" });
                }

                var completedQty = dto.CompletedQuantity ?? item.Quantity;
                item.CompletedQuantity = Math.Min(completedQty, item.Quantity);
                item.KitchenNotes = dto.KitchenNotes;

                if (item.CompletedQuantity >= item.Quantity)
                {
                    item.Status = "Completed";
                    item.CompletedTime = DateTime.Now;
                }

                // Kiểm tra nếu tất cả items đã xong
                var ticket = item.KitchenOrderTicket;
                var allItems = db.KitchenOrderItem.Where(i => i.KitchenOrderTicketId == ticket.Id).ToList();
                
                if (allItems.All(i => i.CompletedQuantity >= i.Quantity))
                {
                    ticket.Status = "Ready";
                    ticket.CompletedTime = DateTime.Now;

                    // Cập nhật Order
                    var order = db.Order.Find(ticket.OrderId);
                    if (order != null)
                    {
                        order.Status = "Ready";
                    }

                    // Tạo notification cho thu ngân
                    var notification = new Notification
                    {
                        Type = "OrderReady",
                        Title = $"Đơn hàng bàn {db.RestaurantTable.Find(ticket.TableId)?.TableNumber} đã sẵn sàng",
                        Message = $"Ticket #{ticket.TicketCode} đã hoàn thành",
                        Data = ticket.TableId.ToString(),
                        CreatedDate = DateTime.Now,
                        IsRead = false,
                        Level = "High"
                    };
                    db.Notification.Add(notification);
                }

                db.SaveChanges();

                return Json(new
                {
                    success = true,
                    message = "Đã cập nhật",
                    itemStatus = item.Status,
                    ticketStatus = ticket.Status
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        /// <summary>
        /// Hoàn thành toàn bộ ticket
        /// </summary>
        [HttpPost]
        [CustomAuthorize("Admin", "Manager", "Kitchen")]
        public JsonResult CompleteTicket(int ticketId)
        {
            try
            {
                var ticket = db.KitchenOrderTicket
                    .Include(t => t.KitchenOrderItem)
                    .FirstOrDefault(t => t.Id == ticketId);

                if (ticket == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy ticket" });
                }

                ticket.Status = "Ready";
                ticket.CompletedTime = DateTime.Now;

                foreach (var item in ticket.KitchenOrderItem)
                {
                    item.Status = "Completed";
                    item.CompletedQuantity = item.Quantity;
                    item.CompletedTime = DateTime.Now;
                }

                // Cập nhật Order
                var order = db.Order.Find(ticket.OrderId);
                if (order != null)
                {
                    order.Status = "Ready";
                }

                // Notification
                var table = db.RestaurantTable.Find(ticket.TableId);
                db.Notification.Add(new Notification
                {
                    Type = "OrderReady",
                    Title = $"Bàn {table?.TableNumber} - Đơn sẵn sàng",
                    Message = $"Ticket #{ticket.TicketCode}",
                    Data = ticket.TableId.ToString(),
                    CreatedDate = DateTime.Now,
                    IsRead = false,
                    Level = "High"
                });

                db.SaveChanges();

                return Json(new { success = true, message = "Đơn hàng đã sẵn sàng phục vụ!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        /// <summary>
        /// Đánh dấu ticket đã phục vụ
        /// </summary>
        [HttpPost]
        [CustomAuthorize("Admin", "Manager", "Kitchen", "Cashier")]
        public JsonResult ServeTicket(int ticketId)
        {
            try
            {
                var ticket = db.KitchenOrderTicket.Find(ticketId);
                if (ticket == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy ticket" });
                }

                ticket.Status = "Completed";

                db.SaveChanges();

                return Json(new { success = true, message = "Đã đánh dấu phục vụ xong" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        /// <summary>
        /// Thay đổi mức ưu tiên
        /// </summary>
        [HttpPost]
        [CustomAuthorize("Admin", "Manager", "Kitchen")]
        public JsonResult UpdatePriority(int ticketId, int priority)
        {
            try
            {
                var ticket = db.KitchenOrderTicket.Find(ticketId);
                if (ticket == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy ticket" });
                }

                ticket.Priority = Math.Max(1, Math.Min(5, priority));
                db.SaveChanges();

                return Json(new { success = true, message = "Đã cập nhật mức ưu tiên" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        /// <summary>
        /// Hủy ticket
        /// </summary>
        [HttpPost]
        [CustomAuthorize("Admin", "Manager")]
        public JsonResult CancelTicket(int ticketId, string reason)
        {
            try
            {
                var ticket = db.KitchenOrderTicket
                    .Include(t => t.KitchenOrderItem)
                    .FirstOrDefault(t => t.Id == ticketId);

                if (ticket == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy ticket" });
                }

                ticket.Status = "Cancelled";
                ticket.SpecialNotes = (ticket.SpecialNotes ?? "") + "\n[Hủy]: " + reason;

                foreach (var item in ticket.KitchenOrderItem)
                {
                    item.Status = "Cancelled";
                }

                db.SaveChanges();

                return Json(new { success = true, message = "Đã hủy ticket" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        #endregion

        #region API Endpoints

        /// <summary>
        /// Lấy dữ liệu hiển thị (polling)
        /// </summary>
        [HttpGet]
        public JsonResult GetDisplayData(string station = "")
        {
            try
            {
                var data = GetKitchenDisplayData(station);
                return Json(new { success = true, data = data });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Lấy thống kê bếp
        /// </summary>
        [HttpGet]
        public JsonResult GetStats()
        {
            try
            {
                var stats = GetKitchenStats();
                return Json(new { success = true, stats = stats });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Lấy chi tiết ticket
        /// </summary>
        [HttpGet]
        public JsonResult GetTicketDetail(int ticketId)
        {
            try
            {
                var ticket = db.KitchenOrderTicket
                    .Include(t => t.KitchenOrderItem.Select(i => i.MenuItem))
                    .Include(t => t.RestaurantTable)
                    .Include(t => t.Employee)
                    .FirstOrDefault(t => t.Id == ticketId);

                if (ticket == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy ticket" });
                }

                var viewModel = MapToTicketViewModel(ticket);
                return Json(new { success = true, ticket = viewModel });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// In ticket
        /// </summary>
        [HttpPost]
        public JsonResult PrintTicket(int ticketId)
        {
            try
            {
                var ticket = db.KitchenOrderTicket.Find(ticketId);
                if (ticket == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy ticket" });
                }

                ticket.IsPrinted = true;
                ticket.PrintCount++;
                db.SaveChanges();

                return Json(new { success = true, message = "Đang in..." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region Station Management

        /// <summary>
        /// Quản lý station
        /// </summary>
        [CustomAuthorize("Admin", "Manager")]
        public ActionResult Stations()
        {
            var stations = db.KitchenStation.OrderBy(s => s.DisplayOrder).ToList();
            var categories = db.MenuItem.Select(m => m.Category).Distinct().ToList();
            var chefs = db.Employee
                .Where(e => e.IsActive && (e.Role.RoleName == "Kitchen" || e.Role.RoleName == "Chef"))
                .ToList();

            var viewModel = new StationManagementViewModel
            {
                Stations = stations.Select(s => new KitchenStationViewModel
                {
                    Id = s.Id,
                    Code = s.Code,
                    Name = s.Name,
                    Description = s.Description,
                    HandledCategories = s.HandledCategories,
                    DisplayColor = s.DisplayColor,
                    DisplayOrder = s.DisplayOrder,
                    IsActive = s.IsActive,
                    PendingTicketCount = db.KitchenOrderTicket.Count(t => t.KitchenStation == s.Code && t.Status == "Pending"),
                    PreparingTicketCount = db.KitchenOrderTicket.Count(t => t.KitchenStation == s.Code && t.Status == "Preparing")
                }).ToList(),
                AvailableCategories = categories,
                AvailableChefs = chefs.Select(c => new ChefViewModel
                {
                    Id = c.Id,
                    FullName = c.FullName,
                    ActiveTicketCount = db.KitchenOrderTicket.Count(t => t.AssignedChefId == c.Id && 
                        (t.Status == "Pending" || t.Status == "Preparing"))
                }).ToList()
            };

            return View(viewModel);
        }

        /// <summary>
        /// Tạo station mới
        /// </summary>
        [HttpPost]
        [CustomAuthorize("Admin", "Manager")]
        public JsonResult CreateStation(StationFormViewModel model)
        {
            try
            {
                if (db.KitchenStation.Any(s => s.Code == model.Code))
                {
                    return Json(new { success = false, message = "Mã station đã tồn tại" });
                }

                var station = new KitchenStation
                {
                    Code = model.Code,
                    Name = model.Name,
                    Description = model.Description,
                    HandledCategories = model.HandledCategories,
                    DisplayColor = model.DisplayColor ?? "#3b82f6",
                    DisplayOrder = model.DisplayOrder,
                    IsActive = model.IsActive
                };

                db.KitchenStation.Add(station);
                db.SaveChanges();

                return Json(new { success = true, message = "Đã tạo station thành công" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        /// <summary>
        /// Cập nhật station
        /// </summary>
        [HttpPost]
        [CustomAuthorize("Admin", "Manager")]
        public JsonResult UpdateStation(StationFormViewModel model)
        {
            try
            {
                var station = db.KitchenStation.Find(model.Id);
                if (station == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy station" });
                }

                station.Code = model.Code;
                station.Name = model.Name;
                station.Description = model.Description;
                station.HandledCategories = model.HandledCategories;
                station.DisplayColor = model.DisplayColor;
                station.DisplayOrder = model.DisplayOrder;
                station.IsActive = model.IsActive;

                db.SaveChanges();

                return Json(new { success = true, message = "Đã cập nhật station" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        /// <summary>
        /// Xóa station
        /// </summary>
        [HttpPost]
        [CustomAuthorize("Admin", "Manager")]
        public JsonResult DeleteStation(int id)
        {
            try
            {
                var station = db.KitchenStation.Find(id);
                if (station == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy station" });
                }

                db.KitchenStation.Remove(station);
                db.SaveChanges();

                return Json(new { success = true, message = "Đã xóa station" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        #endregion

        #region Helpers

        private KitchenDisplayViewModel GetKitchenDisplayData(string station)
        {
            var query = db.KitchenOrderTicket
                .Include(t => t.KitchenOrderItem.Select(i => i.MenuItem))
                .Include(t => t.RestaurantTable)
                .Include(t => t.Employee)
                .Where(t => t.Status != "Completed" && t.Status != "Cancelled");

            if (!string.IsNullOrEmpty(station))
            {
                query = query.Where(t => t.KitchenStation == station || 
                                        t.KitchenOrderItem.Any(i => i.Station == station));
            }

            var tickets = query
                .OrderByDescending(t => t.Priority)
                .ThenBy(t => t.CreatedTime)
                .ToList();

            return new KitchenDisplayViewModel
            {
                Tickets = tickets.Select(t => MapToTicketViewModel(t)).ToList(),
                Stations = GetStationsWithStats(),
                Stats = GetKitchenStats(),
                CurrentStation = station,
                LastUpdated = DateTime.Now
            };
        }

        private KitchenTicketViewModel MapToTicketViewModel(KitchenOrderTicket ticket)
        {
            return new KitchenTicketViewModel
            {
                Id = ticket.Id,
                TicketCode = ticket.TicketCode,
                OrderId = ticket.OrderId,
                OrderCode = $"DH{ticket.OrderId:D6}",
                TableId = ticket.TableId,
                TableNumber = ticket.RestaurantTable?.TableNumber ?? "",
                Status = ticket.Status,
                Priority = ticket.Priority,
                SpecialNotes = ticket.SpecialNotes,
                CreatedTime = ticket.CreatedTime,
                StartedTime = ticket.StartedTime,
                CompletedTime = ticket.CompletedTime,
                EstimatedMinutes = ticket.EstimatedMinutes,
                AssignedChefId = ticket.AssignedChefId,
                AssignedChefName = ticket.Employee?.FullName,
                KitchenStation = ticket.KitchenStation,
                IsPrinted = ticket.IsPrinted,
                PrintCount = ticket.PrintCount,
                Items = ticket.KitchenOrderItem?.Select(i => new KitchenItemViewModel
                {
                    Id = i.Id,
                    KitchenOrderTicketId = i.KitchenOrderTicketId,
                    OrderDetailId = i.OrderDetailId,
                    MenuItemId = i.MenuItemId,
                    ItemName = i.ItemName,
                    ItemImage = i.MenuItem?.ImageUrl,
                    Quantity = i.Quantity,
                    CompletedQuantity = i.CompletedQuantity,
                    Status = i.Status,
                    CustomerNotes = i.CustomerNotes,
                    KitchenNotes = i.KitchenNotes,
                    StartedTime = i.StartedTime,
                    CompletedTime = i.CompletedTime,
                    Station = i.Station
                }).ToList() ?? new List<KitchenItemViewModel>()
            };
        }

        private List<KitchenStationViewModel> GetStationsWithStats()
        {
            var stations = db.KitchenStation.Where(s => s.IsActive).OrderBy(s => s.DisplayOrder).ToList();
            return stations.Select(s => new KitchenStationViewModel
            {
                Id = s.Id,
                Code = s.Code,
                Name = s.Name,
                Description = s.Description,
                HandledCategories = s.HandledCategories,
                DisplayColor = s.DisplayColor,
                DisplayOrder = s.DisplayOrder,
                IsActive = s.IsActive,
                PendingTicketCount = db.KitchenOrderTicket.Count(t => t.KitchenStation == s.Code && t.Status == "Pending"),
                PreparingTicketCount = db.KitchenOrderTicket.Count(t => t.KitchenStation == s.Code && t.Status == "Preparing"),
                TotalItemCount = db.KitchenOrderItem
                    .Count(i => i.Station == s.Code && 
                               (i.KitchenOrderTicket.Status == "Pending" || i.KitchenOrderTicket.Status == "Preparing"))
            }).ToList();
        }

        private KitchenStatsViewModel GetKitchenStats()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var allTicketsToday = db.KitchenOrderTicket
                .Where(t => t.CreatedTime >= today && t.CreatedTime < tomorrow)
                .ToList();

            var completedToday = allTicketsToday.Where(t => t.Status == "Completed" || t.Status == "Ready").ToList();
            var avgPrepTime = completedToday.Any() 
                ? completedToday.Where(t => t.StartedTime.HasValue && t.CompletedTime.HasValue)
                    .Average(t => (t.CompletedTime.Value - t.StartedTime.Value).TotalMinutes)
                : 0;

            return new KitchenStatsViewModel
            {
                TotalPendingTickets = db.KitchenOrderTicket.Count(t => t.Status == "Pending"),
                TotalPreparingTickets = db.KitchenOrderTicket.Count(t => t.Status == "Preparing"),
                TotalReadyTickets = db.KitchenOrderTicket.Count(t => t.Status == "Ready"),
                TotalCompletedToday = completedToday.Count,
                OverdueTickets = db.KitchenOrderTicket.Count(t => 
                    (t.Status == "Pending" || t.Status == "Preparing") &&
                    DbFunctions.DiffMinutes(t.StartedTime ?? t.CreatedTime, DateTime.Now) > t.EstimatedMinutes),
                AveragePreparationTime = Math.Round(avgPrepTime, 1),
                TotalItemsToday = db.KitchenOrderItem
                    .Count(i => i.KitchenOrderTicket.CreatedTime >= today && i.KitchenOrderTicket.CreatedTime < tomorrow)
            };
        }

        private string GetStationForCategory(string category)
        {
            var station = db.KitchenStation
                .FirstOrDefault(s => s.IsActive && s.HandledCategories.Contains(category));
            return station?.Code ?? "MAIN";
        }

        private int? GetCurrentEmployeeId()
        {
            return HttpContext.Session.GetString("EmployeeId") as int?;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}
