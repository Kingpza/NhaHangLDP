using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using Newtonsoft.Json;
using NhaHangLDP.Models;

namespace NhaHangLDP.Controllers
{
    public class CashierController : Controller
    {
        private NhaHangLDPEntities db = new NhaHangLDPEntities();

        #region Shift Management - Quản lý ca làm việc với Database

        public ActionResult OpenShift()
        {
            var activeShift = GetActiveShiftFromDatabase();
            if (activeShift != null)
            {
                return RedirectToAction("Dashboard");
            }
            return View();
        }
        [HttpPost]
        public ActionResult StartShift(decimal openingAmount, string notes)
        {
            try
            {
                if (openingAmount <= 0)
                {
                    return Json(new { success = false, message = "Số tiền mặt đầu ca phải lớn hơn 0!" });
                }

                var existingShift = GetActiveShiftFromDatabase();
                if (existingShift != null)
                {
                    return Json(new { success = false, message = "Đã có ca làm việc đang hoạt động!" });
                }

                var cashierShift = new CashierShift
                {
                    CashierId = GetCurrentCashierIdFromSession(), 
                    StartTime = DateTime.Now,
                    InitialCash = openingAmount,
                    Status = "Active",
                    TotalRevenue = 0
                };

                db.CashierShift.Add(cashierShift);
                db.SaveChanges();
                Session["ActiveShiftId"] = cashierShift.Id;
                Session["ShiftStartTime"] = cashierShift.StartTime;

                return Json(new
                {
                    success = true,
                    message = "Mở ca thành công!",
                    shiftId = cashierShift.Id,
                    openingAmount = openingAmount
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }
        [HttpPost]
        
        public ActionResult CloseShift()
        {
            try
            {
                var activeShift = db.CashierShift
                                    .Include(s => s.ShiftSupportStaff)
                                    .FirstOrDefault(s => s.Status == "Active");

                if (activeShift == null)
                {
                    return Json(new { success = false, message = "Không có ca làm việc nào đang hoạt động!" });
                }

                var shiftRevenue = CalculateShiftRevenue(activeShift.Id);

                activeShift.EndTime = DateTime.Now;
                activeShift.FinalCash = activeShift.InitialCash + shiftRevenue.CashRevenue;
                activeShift.TotalRevenue = shiftRevenue.TotalRevenue;
                activeShift.Status = "Closed";

                db.SaveChanges();

                Session.Remove("ActiveShiftId");
                Session.Remove("ShiftStartTime");

                return Json(new
                {
                    success = true,
                    message = "Đóng ca thành công!",
                    summary = new
                    {
                        TotalOrders = shiftRevenue.OrderCount,
                        TotalRevenue = shiftRevenue.TotalRevenue,
                        CashInHand = activeShift.FinalCash
                    },
                    printUrl = Url.Action("PrintShiftRevenueReport", "Cashier", new { shiftId = activeShift.Id })
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }
        public ActionResult PrintShiftRevenueReport(int? shiftId)
        {
            if (!shiftId.HasValue)
            {
                return RedirectToAction("Dashboard");
            }

            var shift = db.CashierShift
                .Include(s => s.Employee)
                .Include(s => s.ShiftSupportStaff.Select(ss => ss.Employee))
                .FirstOrDefault(s => s.Id == shiftId.Value);

            if (shift == null)
            {
                return RedirectToAction("Dashboard");
            }

            var billQuery = db.Bill.Where(b => b.Order.ShiftId == shiftId.Value && b.Status == "Paid");

            var totalRevenue = billQuery.Sum(b => (decimal?)b.FinalAmount) ?? 0;
            var totalOrders = db.Order.Count(o => o.ShiftId == shiftId.Value);
            var completedOrders = db.Order.Count(o => o.ShiftId == shiftId.Value && o.Status == "Completed");
            var cancelledOrders = db.Order.Count(o => o.ShiftId == shiftId.Value && o.Status == "Cancelled");

            var paymentMethods = billQuery
                .GroupBy(b => b.PaymentMethod)
                .Select(g => new PaymentMethodSummary
                {
                    MethodName = g.Key,
                    OrderCount = g.Count(),
                    TotalAmount = g.Sum(b => b.FinalAmount)
                })
                .ToList();

            paymentMethods.ForEach(p =>
                p.Percentage = (totalRevenue > 0) ? (p.TotalAmount / totalRevenue) * 100 : 0
            );

            var topOrders = db.Bill
                .Where(b => b.Order.ShiftId == shiftId.Value && b.Order.Status == "Completed")
                .OrderByDescending(b => b.FinalAmount)
                .Take(10)
                .Select(b => new OrderSummary
                {
                    OrderId = b.OrderId.ToString(),
                    TableName = b.Order.RestaurantTable.TableNumber,
                    OrderTime = b.Order.OrderTime,
                    PaymentMethod = b.PaymentMethod,
                    TotalAmount = b.FinalAmount
                })
                .ToList();

            var supportNames = shift.ShiftSupportStaff
                                    .Select(ss => ss.Employee.FullName)
                                    .ToList();

            var appSettings = db.AppSetting.ToList();

            var viewModel = new ShiftRevenueReportViewModel
            {
                ShiftId = shift.Id.ToString(),
                CashierName = shift.Employee.FullName,
                SupportStaffNames = supportNames,
                ShiftStartTime = shift.StartTime,
                ShiftEndTime = shift.EndTime ?? DateTime.Now,
                OpeningAmount = shift.InitialCash,
                CashInHand = shift.FinalCash ?? (shift.InitialCash + paymentMethods.FirstOrDefault(p => p.MethodName.ToLower() == "cash")?.TotalAmount ?? 0),
                TotalRevenue = totalRevenue,
                TotalOrders = totalOrders,
                CompletedOrders = completedOrders,
                CancelledOrders = cancelledOrders,
                PaymentMethods = paymentMethods,
                TopOrders = topOrders,
                RestaurantName = appSettings.FirstOrDefault(a => a.SettingKey == "RestaurantName")?.SettingValue ?? "LDP POS System",
                RestaurantAddress = appSettings.FirstOrDefault(a => a.SettingKey == "Address")?.SettingValue ?? "N/A",
                RestaurantPhone = appSettings.FirstOrDefault(a => a.SettingKey == "PhoneNumber")?.SettingValue ?? "N/A",
                RestaurantTaxCode = "0123456789"
            };

            return View(viewModel);
        }

        #endregion

        #region Main Pages - Các trang chính
        public ActionResult Dashboard()
        {
            var activeShift = db.CashierShift
                                .Include(cs => cs.Employee)
                                .Include(s => s.ShiftSupportStaff.Select(ss => ss.Employee))
                                .FirstOrDefault(cs => cs.Status == "Active");

            if (activeShift == null)
            {
                return RedirectToAction("OpenShift");
            }
            Session["ActiveShiftId"] = activeShift.Id;
            Session["ShiftStartTime"] = activeShift.StartTime;
            Session["CashierName"] = activeShift.Employee?.FullName ?? "Thu Ngân";

            var viewModel = new CashierDashboardViewModel
            {
                ActiveOrderCount = db.Order.Count(o =>
                    (o.Status == "Pending" || o.Status == "Preparing" || o.Status == "Ready")
                    && o.ShiftId == activeShift.Id
                ),
                PendingOrderCount = db.Order.Count(o =>
                    o.Status == "Pending"
                    && o.ShiftId == activeShift.Id
                ),
                TotalOrdersToday = db.Order.Count(o => o.ShiftId == activeShift.Id),

                TodayRevenue = db.Bill
                    .Where(b => b.Order.ShiftId == activeShift.Id && b.Status == "Paid")
                    .Sum(b => (decimal?)b.FinalAmount) ?? 0,
                OccupiedTables = db.RestaurantTable.Count(t => t.Status == "Occupied"),
                TotalTables = db.RestaurantTable.Count(),
                ReservedTables = db.RestaurantTable.Count(t => t.Status == "Reserved"),
                AvailableTables = db.RestaurantTable.Count(t => t.Status == "Available"),
                ActiveShift = activeShift,
                ShiftStartTime = activeShift.StartTime,
                HasActiveShift = true,
                CashierName = activeShift.Employee?.FullName ?? "Thu Ngân"
            };

            return View(viewModel);
        }

        public ActionResult Orders()
        {
            if (GetActiveShiftFromDatabase() == null)
                return RedirectToAction("OpenShift");
            return View();
        }
        public ActionResult TableArea()
        {
            if (GetActiveShiftFromDatabase() == null)
                return RedirectToAction("OpenShift");

            try
            {
                var tableAreasWithTables = db.TableArea
                  .Include(a => a.RestaurantTable.Select(t => t.Order.Select(o => o.OrderDetail)))
                     .OrderBy(a => a.Name)
                  .ToList();
                var viewModel = new TableAreasViewModel
                {
                    LastUpdated = DateTime.Now
                };

                foreach (var area in tableAreasWithTables)
                {
                    var areaViewModel = new TableAreaViewModel
                    {
                        Id = area.Id,
                        Name = area.Name
                    };

                    foreach (var table in area.RestaurantTable.OrderBy(t => t.TableNumber))
                    {
                        var activeOrder = table.Order
                          .Where(o => o.Status == "Pending" || o.Status == "Preparing" || o.Status == "Ready")
                               .OrderByDescending(o => o.OrderTime)
                      .FirstOrDefault();

                        var tableViewModel = new TableViewModel
                        {
                            Id = table.Id,
                            TableNumber = table.TableNumber,
                            Capacity = table.Capacity,
                            Status = table.Status,
                            ActiveOrderId = activeOrder?.Id,
                            OrderTime = activeOrder?.OrderTime,
                            OrderTimeDisplay = activeOrder?.OrderTime.ToString("HH:mm"),
                            ItemCount = activeOrder?.OrderDetail.Count ?? 0,
                            CustomerCount = 0,
                            StatusDisplay = table.Status == "Available" ? "Trống" :
                            table.Status == "Occupied" ? "Có khách" :
                            table.Status == "Reserved" ? "Đã đặt" : "Không xác định",
                            StatusClass = table.Status?.ToLower() ?? "unavailable"
                        };

                        areaViewModel.Tables.Add(tableViewModel);
                    }

                    viewModel.TableAreas.Add(areaViewModel);
                }

                var allTables = viewModel.TableAreas.SelectMany(a => a.Tables).ToList();
                viewModel.TotalTables = allTables.Count;
                viewModel.AvailableTables = allTables.Count(t => t.Status == "Available");
                viewModel.OccupiedTables = allTables.Count(t => t.Status == "Occupied");
                viewModel.ReservedTables = allTables.Count(t => t.Status == "Reserved");

                return View(viewModel);
            }
            catch (Exception ex)
            {
                var emptyModel = new TableAreasViewModel
                {
                    LastUpdated = DateTime.Now
                };

                TempData["Error"] = "Có lỗi xảy ra khi tải danh sách bàn: " + ex.Message;
                return View(emptyModel);
            }
        }

        public ActionResult Menu(int? tableId)
        {
            if (GetActiveShiftFromDatabase() == null)
                return RedirectToAction("OpenShift");

            ViewBag.TableId = tableId;
            return View();
        }
        [HttpGet]
        public JsonResult GetMenuItems(string category = "")
        {
            try
            {
                var query = db.MenuItem.Where(m => m.IsAvailable == true);

                if (!string.IsNullOrEmpty(category) && category != "all")
                {
                    query = query.Where(m => m.Category == category);
                }

                var menuItems = query
          .OrderBy(m => m.Category)
            .ThenBy(m => m.Name)
                      .Select(m => new
                      {
                          id = m.Id,
                          name = m.Name,
                          category = m.Category,
                          price = m.Price,
                          description = m.Description,
                          imageUrl = m.ImageUrl,
                          preparationTime = m.PreparationTime,
                          isAvailable = m.IsAvailable
                      })
               .ToList();

                return Json(new { success = true, items = menuItems }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi tải menu: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        public ActionResult SalesPoint()
        {
            if (GetActiveShiftFromDatabase() == null)
                return RedirectToAction("OpenShift");
            return View();
        }

        public ActionResult ShiftDetails(int? shiftId)
        {
            var activeShift = GetActiveShiftFromDatabase();
            if (activeShift == null)
                return RedirectToAction("OpenShift");

            try
            {
                var targetShift = shiftId.HasValue
                    ? db.CashierShift.Include(s => s.Employee).FirstOrDefault(s => s.Id == shiftId.Value)
                    : activeShift;

                if (targetShift == null)
                {
                    TempData["Error"] = "Không tìm thấy thông tin ca làm việc!";
                    return RedirectToAction("Dashboard");
                }
                var ordersInShift = db.Order
                    .Include(o => o.OrderDetail.Select(od => od.MenuItem))
                    .Include(o => o.RestaurantTable)
                    .Include(o => o.Bill) 
                    .Include(o => o.Employee)
                    .Where(o => o.ShiftId == targetShift.Id)
                    .OrderByDescending(o => o.OrderTime)
                    .ToList();
                var viewModel = new ShiftDetailsViewModel
                {
                    ShiftInfo = targetShift,
                    TotalOrders = ordersInShift.Count,
                    CompletedOrders = ordersInShift.Count(o => o.Status == "Completed"),
                    CancelledOrders = ordersInShift.Count(o => o.Status == "Cancelled"),
                    TotalRevenue = ordersInShift
                        .Where(o => o.Bill.Any(b => b.Status == "Paid"))
                        .Sum(o => o.Bill.Where(b => b.Status == "Paid").Sum(b => b.FinalAmount)),

                    Orders = ordersInShift.Select(order => {

                        var paidBill = order.Bill.FirstOrDefault(b => b.Status == "Paid");

                        return new ShiftOrderViewModel
                        {
                            Id = order.Id,
                            TableNumber = order.RestaurantTable.TableNumber,
                            OrderTime = order.OrderTime,
                            Status = order.Status.ToLower(),
                            StatusDisplay = GetStatusTextVietnamese(order.Status),
                            TotalAmount = order.OrderDetail.Sum(od => od.Quantity * od.PriceAtTime),
                            PaymentMethod = paidBill?.PaymentMethod ?? "",
                            IsPaid = (paidBill != null),
                            ItemCount = order.OrderDetail.Count,
                            CashierName = order.Employee?.FullName ?? "N/A",
                            BillId = paidBill?.Id, 
                            Items = order.OrderDetail.Select(od => new ShiftOrderItemViewModel
                            {
                                Name = od.MenuItem.Name,
                                Quantity = od.Quantity,
                                Price = od.PriceAtTime,
                                Description = od.MenuItem.Description
                            }).ToList()
                        };
                    }).ToList()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi tải chi tiết ca: " + ex.Message;
                return RedirectToAction("Dashboard");
            }
        }
        public ActionResult PrintBill(string orderId)
        {
            if (string.IsNullOrEmpty(orderId))
            {
                return HttpNotFound("Không có mã đơn hàng.");
            }
            int numericOrderId;
            if (!int.TryParse(orderId.Replace("DH", ""), out numericOrderId))
            {
                if (!int.TryParse(orderId, out numericOrderId))
                {
                    return HttpNotFound("Mã đơn hàng không hợp lệ: " + orderId);
                }
            }

            try
            {
                var order = db.Order
                               .Include(o => o.OrderDetail.Select(od => od.MenuItem))
                               .Include(o => o.RestaurantTable)
                               .Include(o => o.Bill)
                               .Include(o => o.Employee)
                               .FirstOrDefault(o => o.Id == numericOrderId); 

                if (order == null)
                {
                    return HttpNotFound("Không tìm thấy đơn hàng.");
                }
                var bill = order.Bill.FirstOrDefault(b => b.Status == "Paid");
                if (bill == null)
                {
                    // Nếu chưa thanh toán, vẫn có thể cho in "Phiếu tạm tính"
                    // Ở đây chúng ta giả định chỉ in khi đã thanh toán
                    // return Content("Đơn hàng này chưa được thanh toán.");
                }

                var subtotal = order.OrderDetail.Sum(od => od.Quantity * od.PriceAtTime);
                var vat = Math.Round(subtotal * 0.1m); 
                var total = subtotal + vat;
                var invoiceViewModel = new InvoiceViewModel
                {
                    OrderId = order.Id.ToString("D6"), 
                    BillId = bill?.Id.ToString() ?? "N/A",
                    TableNumber = order.RestaurantTable.TableNumber,
                    OrderTime = order.OrderTime,
                    BillDate = bill?.BillDate ?? DateTime.Now,
                    CashierName = order.Employee?.FullName ?? (bill?.Employee?.FullName ?? "N/A"),
                    PaymentMethod = GetPaymentMethodText(bill?.PaymentMethod ?? "Chưa thanh toán"),
                    Items = order.OrderDetail.Select(od => new InvoiceItemViewModel
                    {
                        ItemName = od.MenuItem.Name,
                        Quantity = od.Quantity,
                        Price = od.PriceAtTime,
                        Total = od.Quantity * od.PriceAtTime
                    }).ToList(),
                    Subtotal = subtotal,
                    VAT = vat,
                    TotalAmount = bill?.FinalAmount ?? total 
                };
                return View("PrintBill", invoiceViewModel);
            }
            catch (Exception ex)
            {
                return Content("Đã xảy ra lỗi khi tạo hóa đơn: " + ex.Message);
            }
        }

        #endregion

        #region Table Management - Quản lý bàn với Database
        [HttpGet]
        public ActionResult GetTableStatus(int tableId)
        {
            try
            {
                if (GetActiveShiftFromDatabase() == null)
                {
                    return Json(new { success = false, message = "Vui lòng mở ca trước!" }, JsonRequestBehavior.AllowGet);
                }
                var table = db.RestaurantTable
           .Include(t => t.Order.Select(o => o.OrderDetail))
                   .Include(t => t.TableArea)
             .FirstOrDefault(t => t.Id == tableId);

                if (table == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy bàn!" }, JsonRequestBehavior.AllowGet);
                }
                var activeOrder = table.Order
                    .Where(o => o.Status == "Pending" || o.Status == "Preparing")
                .OrderByDescending(o => o.OrderTime)
                     .FirstOrDefault();

                var tableStatus = new
                {
                    TableId = table.Id,
                    TableNumber = table.TableNumber,
                    Status = table.Status,
                    Capacity = table.Capacity,
                    OrderId = activeOrder?.Id,
                    OrderTime = activeOrder?.OrderTime.ToString("HH:mm"),
                    ItemCount = activeOrder?.OrderDetail.Count ?? 0,
                    LastUpdate = DateTime.Now
                };

                return Json(new { success = true, data = tableStatus }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        [HttpPost]
        public ActionResult UpdateTableStatus(int tableId, string status)
        {
            try
            {
                if (GetActiveShiftFromDatabase() == null)
                {
                    return Json(new { success = false, message = "Vui lòng mở ca trước!" });
                }

                var allowedStatuses = new[] { "Available", "Occupied", "Reserved" };
                if (!allowedStatuses.Contains(status))
                {
                    return Json(new { success = false, message = "Trạng thái bàn không hợp lệ!" });
                }

                var table = db.RestaurantTable.Find(tableId);
                if (table == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy bàn!" });
                }

                table.Status = status;
                db.SaveChanges();

                return Json(new
                {
                    success = true,
                    message = $"Đã cập nhật trạng thái bàn {table.TableNumber} thành công!",
                    data = new { tableId = table.Id, status = status }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        #endregion

        #region Order Management - Quản lý đơn hàng với Database và Inventory Check

        [HttpPost]
        public ActionResult CreateOrder(int tableId, string orderItems, string customerNote)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var activeShift = GetActiveShiftFromDatabase();
                    if (activeShift == null)
                    {
                        return Json(new { success = false, message = "Vui lòng mở ca trước khi tạo đơn hàng!" });
                    }

                    if (string.IsNullOrEmpty(orderItems))
                    {
                        return Json(new { success = false, message = "Vui lòng chọn ít nhất một món!" });
                    }

                    List<OrderItemDto> items;
                    try
                    {
                        items = JsonConvert.DeserializeObject<List<OrderItemDto>>(orderItems);
                    }
                    catch (JsonException)
                    {
                        return Json(new { success = false, message = "Dữ liệu đơn hàng không hợp lệ!" });
                    }

                    if (items == null || !items.Any())
                    {
                        return Json(new { success = false, message = "Dữ liệu đơn hàng không hợp lệ!" });
                    }
                    var table = db.RestaurantTable.Find(tableId);
                    if (table == null)
                    {
                        return Json(new { success = false, message = "Bàn không tồn tại!" });
                    }
                    var existingOrder = db.Order
                    .Where(o => o.TableId == tableId && (o.Status == "Pending" || o.Status == "Preparing"))
                .FirstOrDefault();

                    Order order;
                    if (existingOrder != null)
                    {
                        order = existingOrder;
                    }
                    else
                    {
                        order = new Order
                        {
                            TableId = tableId,
                            WaiterId = GetCurrentCashierIdFromSession(),
                            ShiftId = activeShift.Id,
                            OrderTime = DateTime.Now,
                            Status = "Pending"
                        };
                        db.Order.Add(order);
                        db.SaveChanges(); 
                        table.Status = "Occupied";
                    }
                    var stockErrors = new List<string>();
                    var inventoryUpdates = new List<InventoryUpdate>();

                    foreach (var item in items)
                    {
                        var menuItem = db.MenuItem.Find(item.Id);
                        if (menuItem == null || !menuItem.IsAvailable)
                        {
                            stockErrors.Add($"Món {item.Name} không khả dụng");
                            continue;
                        }
                        var requiredIngredients = db.MenuItemIngredient
                       .Include(mi => mi.Ingredient)
                      .Where(mi => mi.MenuItemId == item.Id)
                        .ToList();

                        foreach (var reqIngredient in requiredIngredients)
                        {
                            var totalRequired = reqIngredient.RequiredQuantity * item.Quantity;
                            if (reqIngredient.Ingredient.AvailableStock < totalRequired)
                            {
                                stockErrors.Add($"Không đủ {reqIngredient.Ingredient.Name} cho món {menuItem.Name} (cần {totalRequired:N2} {reqIngredient.Unit}, còn {reqIngredient.Ingredient.AvailableStock:N2})");
                            }
                            else
                            {
                                inventoryUpdates.Add(new InventoryUpdate
                                {
                                    IngredientId = reqIngredient.Ingredient.Id,
                                    RequiredQuantity = totalRequired
                                });
                            }
                        }

                        if (stockErrors.Any()) continue;
                        var existingDetail = db.OrderDetail
                            .FirstOrDefault(od => od.OrderId == order.Id && od.MenuItemId == item.Id);

                        if (existingDetail != null)
                        {
                            existingDetail.Quantity += item.Quantity;
                        }
                        else
                        {
                            var orderDetail = new OrderDetail
                            {
                                OrderId = order.Id,
                                MenuItemId = item.Id,
                                Quantity = item.Quantity,
                                PriceAtTime = menuItem.Price,
                                Notes = customerNote
                            };
                            db.OrderDetail.Add(orderDetail);
                        }
                    }

                    if (stockErrors.Any())
                    {
                        transaction.Rollback();
                        return Json(new { success = false, message = "Lỗi tồn kho:\n" + string.Join("\n", stockErrors) });
                    }

                    foreach (var update in inventoryUpdates)
                    {
                        var ingredient = db.Ingredient.Find(update.IngredientId);
                        if (ingredient != null)
                        {
                            ingredient.AvailableStock -= update.RequiredQuantity;
                        }
                    }

                    db.SaveChanges();
                    transaction.Commit();

                    return Json(new
                    {
                        success = true,
                        message = "Đơn hàng đã được tạo thành công!",
                        orderId = order.Id
                    });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
                }
            }
        }
        [HttpPost]
        public ActionResult UpdateOrderStatus(int orderId, string status)
        {
            try
            {
                if (GetActiveShiftFromDatabase() == null)
                {
                    return Json(new { success = false, message = "Vui lòng mở ca trước!" });
                }

                var allowedStatuses = new[] { "Pending", "Preparing", "Ready", "Completed", "Cancelled" };
                if (!allowedStatuses.Contains(status))
                {
                    return Json(new { success = false, message = "Trạng thái đơn hàng không hợp lệ!" });
                }

                var order = db.Order.Include(o => o.RestaurantTable).FirstOrDefault(o => o.Id == orderId);
                if (order == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng!" });
                }

                order.Status = status;

                // Nếu đơn hàng hoàn thành hoặc hủy, cập nhật trạng thái bàn
                if (status == "Completed" || status == "Cancelled")
                {
                    order.RestaurantTable.Status = "Available";
                }

                db.SaveChanges();

                return Json(new
                {
                    success = true,
                    message = $"Đã cập nhật trạng thái đơn hàng thành {GetStatusTextVietnamese(status)}!",
                    data = new { orderId = order.Id, status = status }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        #endregion

        #region Payment & Invoice - Thanh toán với Database Transaction

        [HttpPost]
        public ActionResult ProcessPayment(int orderId, string paymentMethod, decimal receivedAmount, decimal totalAmount)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var activeShift = GetActiveShiftFromDatabase();
                    if (activeShift == null)
                    {
                        return Json(new { success = false, message = "Vui lòng mở ca trước khi thanh toán!" });
                    }

                    var order = db.Order
                    .Include(o => o.OrderDetail.Select(od => od.MenuItem))
                     .Include(o => o.RestaurantTable)
                       .FirstOrDefault(o => o.Id == orderId);

                    if (order == null)
                    {
                        return Json(new { success = false, message = "Không tìm thấy đơn hàng!" });
                    }
                    var calculatedTotal = order.OrderDetail.Sum(od => od.Quantity * od.PriceAtTime);
                    var vat = calculatedTotal * 0.1m;
                    var finalAmount = calculatedTotal + vat;

                    if (paymentMethod == "cash" && receivedAmount < finalAmount)
                    {
                        return Json(new { success = false, message = "Số tiền nhận không đủ để thanh toán!" });
                    }

                    var bill = new Bill
                    {
                        OrderId = order.Id,
                        CashierId = GetCurrentCashierIdFromSession(),
                        BillDate = DateTime.Now,
                        TotalAmount = calculatedTotal,
                        DiscountAmount = 0,
                        FinalAmount = finalAmount,
                        PaymentMethod = paymentMethod,
                        Status = "Paid"
                    };

                    db.Bill.Add(bill);
                    order.Status = "Completed";
                    order.RestaurantTable.Status = "Available";
                    activeShift.TotalRevenue = (activeShift.TotalRevenue ?? 0) + finalAmount;
                    db.SaveChanges();
                    transaction.Commit();
                    return Json(new
                    {
                        success = true,
                        message = "Thanh toán thành công!",
                        billId = bill.Id,
                        changeAmount = Math.Max(0, receivedAmount - finalAmount)
                    });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
                }
            }
        }

        [HttpPost]
        public ActionResult GenerateInvoice(int orderId)
        {
            try
            {
                if (GetActiveShiftFromDatabase() == null)
                {
                    return Json(new { success = false, message = "Vui lòng mở ca trước khi tạo hóa đơn!" });
                }

                     var order = db.Order
                    .Include(o => o.OrderDetail.Select(od => od.MenuItem))
                    .Include(o => o.RestaurantTable)
                    .Include(o => o.Bill)
                    .FirstOrDefault(o => o.Id == orderId);

                if (order == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng!" });
                }

                var bill = order.Bill.FirstOrDefault();
                var invoiceId = "HD" + DateTime.Now.ToString("yyyyMMddHHmmss");

                return Json(new
                {
                    success = true,
                    message = "Tạo hóa đơn thành công!",
                    invoiceId = invoiceId,
                    billId = bill?.Id
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        #endregion

        #region Orders Management API - Real Database Operations
        [HttpGet]
        public JsonResult GetOrdersData(string status = "all")
        {
            try
            {
                var activeShift = GetActiveShiftFromDatabase();
                if (activeShift == null)
                {
                    return Json(new { success = false, message = "Vui lòng mở ca trước khi xem đơn hàng!" }, JsonRequestBehavior.AllowGet);
                }
                var query = db.Order
                  .Include(o => o.OrderDetail.Select(od => od.MenuItem))
            .Include(o => o.RestaurantTable)
           .Include(o => o.Bill)
       .Where(o => o.ShiftId == activeShift.Id);

                if (status != "all")
                {
                    switch (status.ToLower())
                    {
                        case "incomplete":
                            query = query.Where(o => o.Status == "Pending" || o.Status == "Preparing" || o.Status == "Ready");
                            break;
                        case "completed":
                            query = query.Where(o => o.Status == "Completed" || o.Bill.Any(b => b.Status == "Paid"));
                            break;
                        case "paid":
                            query = query.Where(o => o.Bill.Any(b => b.Status == "Paid"));
                            break;
                        default:
                            query = query.Where(o => o.Status == status);
                            break;
                    }
                }

                var orders = query
                  .OrderByDescending(o => o.OrderTime)
        .ToList()
    .Select(order => new
    {
        id = order.Id.ToString(),
        table = order.RestaurantTable.TableNumber,
        status = order.Status.ToLower(),
        statusText = GetStatusTextVietnamese(order.Status),
        time = order.OrderTime.ToString("HH:mm"),
        paymentStatus = order.Bill.Any(b => b.Status == "Paid") ? "paid" : "unpaid",
        paymentMethod = order.Bill.FirstOrDefault(b => b.Status == "Paid")?.PaymentMethod ?? "",
        items = order.OrderDetail.Select(od => new
        {
            name = od.MenuItem.Name,
            quantity = od.Quantity,
            price = od.PriceAtTime,
            description = od.MenuItem.Description ?? ""
        }).ToList(),
        total = order.OrderDetail.Sum(od => od.Quantity * od.PriceAtTime)
    })
         .ToList();

                return Json(new { success = true, orders = orders }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi lấy dữ liệu đơn hàng: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // GET: Cashier/GetOrderDetail - Lấy chi tiết đơn hàng từ Database
        [HttpGet]
        public JsonResult GetOrderDetail(int orderId)
        {
            try
            {
                var order = db.Order
                       .Include(o => o.OrderDetail.Select(od => od.MenuItem))
               .Include(o => o.RestaurantTable)
             .Include(o => o.Bill)
                            .Include(o => o.Employee)
                    .FirstOrDefault(o => o.Id == orderId);

                if (order == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng!" }, JsonRequestBehavior.AllowGet);
                }

                var bill = order.Bill.FirstOrDefault();
                var subtotal = order.OrderDetail.Sum(od => od.Quantity * od.PriceAtTime);
                var vat = Math.Round(subtotal * 0.1m);
                var total = subtotal + vat;

                var orderDetail = new
                {
                    id = order.Id.ToString(),
                    table = order.RestaurantTable.TableNumber,
                    status = order.Status.ToLower(),
                    statusText = GetStatusTextVietnamese(order.Status),
                    time = order.OrderTime.ToString("HH:mm"),
                    date = order.OrderTime.ToString("dd/MM/yyyy"),
                    paymentStatus = bill?.Status == "Paid" ? "paid" : "unpaid",
                    paymentMethod = bill?.PaymentMethod ?? "",
                    waiter = order.Employee?.FullName ?? "N/A",
                    items = order.OrderDetail.Select(od => new
                    {
                        name = od.MenuItem.Name,
                        quantity = od.Quantity,
                        price = od.PriceAtTime,
                        description = od.MenuItem.Description ?? "",
                        total = od.Quantity * od.PriceAtTime
                    }).ToList(),
                    subtotal = subtotal,
                    vat = vat,
                    total = total,
                    finalAmount = bill?.FinalAmount ?? total
                };

                return Json(new { success = true, order = orderDetail }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi lấy chi tiết đơn hàng: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        [HttpPost]
        public JsonResult UpdateOrderStatusAPI()
        {
            try
            {
                var activeShift = GetActiveShiftFromDatabase();
                if (activeShift == null)
                {
                    return Json(new { success = false, message = "Vui lòng mở ca trước!" });
                }

                // Lấy tham số từ Request
                var orderIdStr = Request.Form["orderId"];
                var status = Request.Form["status"];

                if (string.IsNullOrEmpty(orderIdStr) || string.IsNullOrEmpty(status))
                {
                    return Json(new { success = false, message = "Thiếu thông tin đơn hàng hoặc trạng thái!" });
                }

                if (!int.TryParse(orderIdStr, out int orderId))
                {
                    return Json(new { success = false, message = "Mã đơn hàng không hợp lệ!" });
                }

                // Chuẩn hóa trạng thái - chuyển chữ cái đầu thành hoa
                status = char.ToUpper(status[0]) + status.Substring(1).ToLower();

                var allowedStatuses = new[] { "Pending", "Preparing", "Ready", "Completed", "Cancelled" };
                if (!allowedStatuses.Contains(status))
                {
                    return Json(new { success = false, message = "Trạng thái không hợp lệ!" });
                }

                var order = db.Order
               .Include(o => o.RestaurantTable)
                 .FirstOrDefault(o => o.Id == orderId);

                if (order == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng!" });
                }

                order.Status = status;

                // Cập nhật trạng thái bàn nếu cần
                if (status == "Completed" || status == "Cancelled")
                {
                    order.RestaurantTable.Status = "Available";
                }

                db.SaveChanges();

                return Json(new
                {
                    success = true,
                    message = $"Đã cập nhật trạng thái đơn hàng thành {GetStatusTextVietnamese(status)}!",
                    data = new { orderId = order.Id, status = status }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi cập nhật trạng thái: " + ex.Message });
            }
        }

        // POST: Cashier/CancelOrderAPI - Hủy đơn hàng qua API
        [HttpPost]
        public JsonResult CancelOrderAPI()
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    // Lấy tham số từ Request
                    var orderIdStr = Request.Form["orderId"];

                    if (string.IsNullOrEmpty(orderIdStr))
                    {
                        return Json(new { success = false, message = "Thiếu thông tin đơn hàng!" });
                    }

                    if (!int.TryParse(orderIdStr, out int orderId))
                    {
                        return Json(new { success = false, message = "Mã đơn hàng không hợp lệ!" });
                    }

                    var order = db.Order
             .Include(o => o.RestaurantTable)
                    .Include(o => o.OrderDetail.Select(od => od.MenuItem.MenuItemIngredient.Select(mi => mi.Ingredient)))
             .FirstOrDefault(o => o.Id == orderId);

                    if (order == null)
                    {
                        return Json(new { success = false, message = "Không tìm thấy đơn hàng!" });
                    }
                    if (order.Status == "Completed" || order.Bill.Any(b => b.Status == "Paid"))
                    {
                        return Json(new { success = false, message = "Không thể hủy đơn hàng đã hoàn thành hoặc đã thanh toán!" });
                    }
                    foreach (var detail in order.OrderDetail)
                    {
                        var requiredIngredients = detail.MenuItem.MenuItemIngredient;
                        foreach (var reqIngredient in requiredIngredients)
                        {
                            var returnQuantity = reqIngredient.RequiredQuantity * detail.Quantity;
                            reqIngredient.Ingredient.AvailableStock += returnQuantity;
                        }
                    }
                    order.Status = "Cancelled";
                    order.RestaurantTable.Status = "Available";

                    db.SaveChanges();
                    transaction.Commit();

                    return Json(new
                    {
                        success = true,
                        message = $"Đơn hàng #{order.Id} đã được hủy thành công!"
                    });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return Json(new { success = false, message = "Lỗi khi hủy đơn hàng: " + ex.Message });
                }
            }
        }
        [HttpPost]
        public JsonResult ProcessPaymentAPI()
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var activeShift = GetActiveShiftFromDatabase();
                    if (activeShift == null)
                    {
                        return Json(new { success = false, message = "Vui lòng mở ca trước khi thanh toán!" });
                    }

                    var orderIdStr = Request.Form["orderId"];
                    var paymentMethod = Request.Form["paymentMethod"];
                    var receivedAmountStr = Request.Form["receivedAmount"];

                    if (string.IsNullOrEmpty(orderIdStr) || string.IsNullOrEmpty(paymentMethod) || string.IsNullOrEmpty(receivedAmountStr))
                    {
                        return Json(new { success = false, message = "Thiếu thông tin thanh toán!" });
                    }

                    if (!int.TryParse(orderIdStr, out int orderId))
                    {
                        return Json(new { success = false, message = "Mã đơn hàng không hợp lệ!" });
                    }

                    if (!decimal.TryParse(receivedAmountStr, out decimal receivedAmount))
                    {
                        return Json(new { success = false, message = "Số tiền nhận không hợp lệ!" });
                    }

                    var order = db.Order
                   .Include(o => o.OrderDetail)
                 .Include(o => o.RestaurantTable)
                     .FirstOrDefault(o => o.Id == orderId);

                    if (order == null)
                    {
                        return Json(new { success = false, message = "Không tìm thấy đơn hàng!" });
                    }
                    var subtotal = order.OrderDetail.Sum(od => od.Quantity * od.PriceAtTime);
                    var vat = Math.Round(subtotal * 0.1m);
                    var finalAmount = subtotal + vat;
                    if (paymentMethod.ToLower() == "cash" && receivedAmount < finalAmount)
                    {
                        return Json(new { success = false, message = "Số tiền nhận không đủ để thanh toán!" });
                    }
                    var bill = new Bill
                    {
                        OrderId = order.Id,
                        CashierId = GetCurrentCashierIdFromSession(),
                        BillDate = DateTime.Now,
                        TotalAmount = subtotal,
                        DiscountAmount = 0,
                        FinalAmount = finalAmount,
                        PaymentMethod = paymentMethod,
                        Status = "Paid"
                    };

                    db.Bill.Add(bill);
                    order.Status = "Completed";
                    order.RestaurantTable.Status = "Available";
                    activeShift.TotalRevenue = (activeShift.TotalRevenue ?? 0) + finalAmount;
                    db.SaveChanges();
                    transaction.Commit();
                    var changeAmount = Math.Max(0, receivedAmount - finalAmount);

                    return Json(new
                    {
                        success = true,
                        message = "Thanh toán thành công!",
                        billId = bill.Id,
                        changeAmount = changeAmount,
                        finalAmount = finalAmount
                    });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return Json(new { success = false, message = "Lỗi khi thanh toán: " + ex.Message });
                }
            }
        }

        #endregion
        [HttpGet]
        public JsonResult GetLastPaidOrder()
        {
            try
            {
                var activeShift = GetActiveShiftFromDatabase();
                if (activeShift == null)
                {
                    return Json(new { success = false, message = "Ca làm việc không hoạt động." }, JsonRequestBehavior.AllowGet);
                }
                var lastBill = db.Bill
                    .Where(b => b.Order.ShiftId == activeShift.Id && b.Status == "Paid")
                    .OrderByDescending(b => b.BillDate)
                    .FirstOrDefault();

                if (lastBill != null)
                {
                    return Json(new { success = true, orderId = lastBill.OrderId.ToString() }, JsonRequestBehavior.AllowGet);
                }
                else
                {
                    return Json(new { success = false, message = "Không tìm thấy hóa đơn nào đã thanh toán trong ca này." }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi máy chủ: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        [HttpGet]
        public JsonResult GetDashboardStats()
        {
            try
            {
                var activeShift = GetActiveShiftFromDatabase();
                if (activeShift == null)
                {
                    return Json(new { success = false, message = "Ca làm việc không hoạt động." }, JsonRequestBehavior.AllowGet);
                }

                var stats = new
                {
                    ActiveOrderCount = db.Order.Count(o => (o.Status == "Pending" || o.Status == "Preparing" || o.Status == "Ready") && o.ShiftId == activeShift.Id),
                    PendingOrderCount = db.Order.Count(o => o.Status == "Pending" && o.ShiftId == activeShift.Id),
                    TotalOrdersToday = db.Order.Count(o => o.ShiftId == activeShift.Id),
                    TodayRevenue = db.Bill.Where(b => b.Order.ShiftId == activeShift.Id && b.Status == "Paid").Sum(b => (decimal?)b.FinalAmount) ?? 0,
                    OccupiedTables = db.RestaurantTable.Count(t => t.Status == "Occupied"),
                    TotalTables = db.RestaurantTable.Count()
                };
                return Json(new { success = true, data = stats }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi máy chủ: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        #region Table Management APIs - API cho trang TableArea
        [HttpGet]
        public JsonResult GetTablesData()
        {
            try
            {
                if (GetActiveShiftFromDatabase() == null)
                {
                    return Json(new { success = false, message = "Vui lòng mở ca trước!" }, JsonRequestBehavior.AllowGet);
                }
                var tablesData = db.RestaurantTable
                    .Include(t => t.TableArea)
                    .Include(t => t.Order.Select(o => o.OrderDetail))
                    .OrderBy(t => t.TableArea.Name)
                    .ThenBy(t => t.TableNumber)
                    .ToList()
                    .Select(table => {
                        var activeOrder = table.Order
                            .Where(o => o.Status == "Pending" || o.Status == "Preparing" || o.Status == "Ready")
                            .OrderByDescending(o => o.OrderTime)
                            .FirstOrDefault();
                        return new
                        {
                            id = table.Id,
                            tableNumber = table.TableNumber,
                            capacity = table.Capacity,
                            status = table.Status?.ToLower() ?? "available",
                            statusText = table.Status == "Available" ? "Trống" :
                                       table.Status == "Occupied" ? "Có khách" :
                                       table.Status == "Reserved" ? "Đã đặt" : "Không xác định",
                            areaName = table.TableArea?.Name ?? "Không xác định",
                            activeOrderId = activeOrder?.Id,
                            orderTime = activeOrder?.OrderTime.ToString("HH:mm"),
                            itemCount = activeOrder?.OrderDetail?.Count ?? 0
                        };
                    })
                    .ToList();

                return Json(new { success = true, tables = tablesData }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi tải dữ liệu bàn: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        [HttpPost]
        public JsonResult UpdateTableInfo()
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var activeShift = GetActiveShiftFromDatabase();
                    if (activeShift == null)
                    {
                        return Json(new { success = false, message = "Vui lòng mở ca trước!" });
                    }

                    var tableIdStr = Request.Form["tableId"];
                    var action = Request.Form["action"];
                    var customersStr = Request.Form["customers"];
                    var customerName = Request.Form["customerName"] ?? "";
                    var customerPhone = Request.Form["customerPhone"] ?? "";
                    var notes = Request.Form["notes"] ?? "";
                    var time = Request.Form["time"] ?? "";

                    if (!int.TryParse(tableIdStr, out int tableId))
                    {
                        return Json(new { success = false, message = "Mã bàn không hợp lệ!" });
                    }

                    if (!int.TryParse(customersStr, out int customers) || customers <= 0)
                    {
                        return Json(new { success = false, message = "Số khách không hợp lệ!" });
                    }

                    var table = db.RestaurantTable.Find(tableId);
                    if (table == null)
                    {
                        return Json(new { success = false, message = "Không tìm thấy bàn!" });
                    }

                    if (customers > table.Capacity)
                    {
                        return Json(new { success = false, message = $"Số khách ({customers}) vượt quá sức chứa của bàn ({table.Capacity})!" });
                    }

                    string message = "";

                    if (action == "assign")
                    {
                        if (table.Status != "Available")
                        {
                            return Json(new { success = false, message = "Bàn không ở trạng thái trống!" });
                        }
                        var booking = new Booking
                        {
                            TableId = tableId,
                            CustomerName = customerName,
                            CustomerPhone = customerPhone,
                            NumberOfGuests = customers,
                            BookingDateTime = DateTime.Now,
                            Notes = notes,
                            Status = "Pending", 
                            CreatedDate = DateTime.Now
                        };

                        db.Booking.Add(booking);

                        table.Status = "Occupied";
                        message = $"Đã xếp {customers} khách vào bàn {table.TableNumber}";
                    }
                    else if (action == "reserve")
                    {
                        if (table.Status != "Available")
                        {
                            return Json(new { success = false, message = "Bàn không ở trạng thái trống!" });
                        }

                        if (string.IsNullOrEmpty(time))
                        {
                            return Json(new { success = false, message = "Vui lòng chọn thời gian đặt bàn!" });
                        }
                        DateTime bookingDateTime;
                        if (!DateTime.TryParseExact($"{DateTime.Today:yyyy-MM-dd} {time}", "yyyy-MM-dd HH:mm", 
                            null, System.Globalization.DateTimeStyles.None, out bookingDateTime))
                        {
                            return Json(new { success = false, message = "Thời gian đặt bàn không hợp lệ!" });
                        }
                        if (bookingDateTime <= DateTime.Now)
                        {
                            return Json(new { success = false, message = "Thời gian đặt bàn phải trong tương lai!" });
                        }
                        var booking = new Booking
                        {
                            TableId = tableId,
                            CustomerName = customerName,
                            CustomerPhone = customerPhone,
                            NumberOfGuests = customers,
                            BookingDateTime = bookingDateTime,
                            Notes = notes,
                            Status = "Pending", 
                            CreatedDate = DateTime.Now
                        };
                        db.Booking.Add(booking);

                        table.Status = "Reserved";
                        message = $"Đã đặt bàn {table.TableNumber} cho {customers} khách lúc {time}";
                    }
                    else
                    {
                        return Json(new { success = false, message = "Hành động không hợp lệ!" });
                    }

                    db.SaveChanges();
                    transaction.Commit();

                    return Json(new { success = true, message = message });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
                }
            }
        }

        [HttpPost]
        public JsonResult ConfirmReservationAPI()
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var tableIdStr = Request.Form["tableId"];
                    if (!int.TryParse(tableIdStr, out int tableId))
                    {
                        return Json(new { success = false, message = "Mã bàn không hợp lệ!" });
                    }

                    var table = db.RestaurantTable.Find(tableId);
                    if (table == null)
                    {
                        return Json(new { success = false, message = "Không tìm thấy bàn!" });
                    }

                    if (table.Status != "Reserved")
                    {
                        return Json(new { success = false, message = "Bàn không ở trạng thái đã đặt!" });
                    }
                    var activeBooking = db.Booking
                        .Where(b => b.TableId == tableId && b.Status == "Pending")
                        .OrderByDescending(b => b.BookingDateTime)
                        .FirstOrDefault();

                    if (activeBooking != null)
                    {
                        activeBooking.Status = "Confirmed";
                    }

                    table.Status = "Occupied";
                    db.SaveChanges();
                    transaction.Commit();

                    return Json(new { success = true, message = $"Khách đã đến bàn {table.TableNumber}" });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
                }
            }
        }

        [HttpPost]
        public JsonResult CancelReservationAPI()
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var tableIdStr = Request.Form["tableId"];
                    if (!int.TryParse(tableIdStr, out int tableId))
                    {
                        return Json(new { success = false, message = "Mã bàn không hợp lệ!" });
                    }

                    var table = db.RestaurantTable.Find(tableId);
                    if (table == null)
                    {
                        return Json(new { success = false, message = "Không tìm thấy bàn!" });
                    }

                    if (table.Status != "Reserved")
                    {
                        return Json(new { success = false, message = "Bàn không ở trạng thái đã đặt!" });
                    }
                    var activeBooking = db.Booking
                        .Where(b => b.TableId == tableId && b.Status == "Pending")
                        .OrderByDescending(b => b.BookingDateTime)
                        .FirstOrDefault();

                    if (activeBooking != null)
                    {
                        activeBooking.Status = "Cancelled";
                    }

                    table.Status = "Available";
                    db.SaveChanges();
                    transaction.Commit();

                    return Json(new { success = true, message = $"Đã hủy đặt bàn {table.TableNumber}" });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
                }
            }
        }
        [HttpPost]
        public JsonResult CheckoutTableAPI()
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var tableIdStr = Request.Form["tableId"];
                    if (!int.TryParse(tableIdStr, out int tableId))
                    {
                        return Json(new { success = false, message = "Mã bàn không hợp lệ!" });
                    }

                    var table = db.RestaurantTable
                        .Include(t => t.Order.Select(o => o.Bill))
                        .FirstOrDefault(t => t.Id == tableId);

                    if (table == null)
                    {
                        return Json(new { success = false, message = "Không tìm thấy bàn!" });
                    }

                    if (table.Status != "Occupied")
                    {
                        return Json(new { success = false, message = "Bàn không có khách để checkout!" });
                    }
                    var unpaidOrder = table.Order
                        .Where(o => o.Status != "Completed" && o.Status != "Cancelled")
                        .FirstOrDefault();

                    if (unpaidOrder != null)
                    {
                        return Json(new { success = false, message = "Bàn còn đơn hàng chưa hoàn thành, vui lòng thanh toán trước!" });
                    }
                    var activeBooking = db.Booking
                        .Where(b => b.TableId == tableId && (b.Status == "Confirmed" || b.Status == "Pending"))
                        .OrderByDescending(b => b.BookingDateTime)
                        .FirstOrDefault();

                    if (activeBooking != null)
                    {
                        activeBooking.Status = "Completed";
                    }

                    table.Status = "Available";
                    db.SaveChanges();
                    transaction.Commit();

                    return Json(new { success = true, message = $"Bàn {table.TableNumber} đã được trả" });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
                }
            }
        }

        #endregion
        [HttpGet]
        public JsonResult GetAvailableEmployees()
        {
            try
            {
                var activeShift = db.CashierShift.FirstOrDefault(s => s.Status == "Active");

                if (activeShift == null)
                {
                    return Json(new List<object>(), JsonRequestBehavior.AllowGet);
                }

                var currentEmployeeId = activeShift.CashierId;

                var availableRoleIds = new List<int> { 1, 2, 3, 4, 5 };

                var employees = db.Employee
                    .Where(e => e.IsActive
                                && availableRoleIds.Contains(e.RoleId)
                                && e.Id != currentEmployeeId)
                    .Select(e => new {
                        id = e.Id,
                        fullName = e.FullName
                    })
                    .ToList();

                return Json(employees, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                Response.StatusCode = 500;
                return Json(new { message = "Lỗi máy chủ: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
        [HttpPost]
        public async Task<JsonResult> RemoveSupportEmployee(int shiftId, int employeeId)
        {
            try
            {
                var supportStaffEntry = await db.ShiftSupportStaff
                    .FirstOrDefaultAsync(s => s.CashierShiftId == shiftId && s.EmployeeId == employeeId);

                if (supportStaffEntry == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy nhân viên hỗ trợ này trong ca." });
                }

                db.ShiftSupportStaff.Remove(supportStaffEntry);
                await db.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }
        [HttpPost]
        public async Task<JsonResult> AddSupportEmployee(int shiftId, int employeeId)
        {
            try
            {
                var activeShift = await db.CashierShift
                    .Include(s => s.ShiftSupportStaff)
                    .FirstOrDefaultAsync(s => s.Id == shiftId && s.EndTime == null);

                if (activeShift == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy ca làm việc." });
                }

                bool isAlreadyInShift = activeShift.CashierId == employeeId ||
                                        activeShift.ShiftSupportStaff.Any(s => s.EmployeeId == employeeId);

                if (isAlreadyInShift)
                {
                    return Json(new { success = false, message = "Nhân viên đã có trong ca." });
                }

                var supportStaff = new ShiftSupportStaff
                {
                    CashierShiftId = shiftId,
                    EmployeeId = employeeId
                };

                db.ShiftSupportStaff.Add(supportStaff);
                await db.SaveChangesAsync();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Đã xảy ra lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<JsonResult> HandoverShift(int shiftId, int newEmployeeId)
        {
            try
            {
                var shift = await db.CashierShift
                    .Include(s => s.ShiftSupportStaff)
                    .FirstOrDefaultAsync(s => s.Id == shiftId && s.EndTime == null);

                if (shift == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy ca làm việc." });
                }

                if (shift.ShiftSupportStaff != null && shift.ShiftSupportStaff.Any())
                {
                    db.ShiftSupportStaff.RemoveRange(shift.ShiftSupportStaff);
                }

                shift.CashierId = newEmployeeId;
                await db.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi hệ thống khi giao ca: " + ex.Message });
            }
        }

        #region Helper Classes

        public class OrderItemDto
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int Quantity { get; set; }
            public decimal Price { get; set; }
        }

        public class InventoryUpdate
        {
            public int IngredientId { get; set; }
            public decimal RequiredQuantity { get; set; }
        }

        public class ShiftRevenueData
        {
            public decimal TotalRevenue { get; set; }
            public decimal CashRevenue { get; set; }
            public int OrderCount { get; set; }
        }

        public class SplitItemDto
        {
            public int OrderDetailId { get; set; }
            public int Quantity { get; set; }
        }

        #endregion

        #region Helper Methods - Database Operations

        private CashierShift GetActiveShiftFromDatabase()
        {
            var activeShiftId = Session["ActiveShiftId"] as int?;
            if (activeShiftId.HasValue)
            {
                return db.CashierShift.FirstOrDefault(s => s.Id == activeShiftId.Value && s.Status == "Active");
            }

            var cashierId = GetCurrentCashierIdFromSession();
            return db.CashierShift.FirstOrDefault(s => s.CashierId == cashierId && s.Status == "Active");
        }

        private int GetCurrentCashierIdFromSession()
        {
            return Session["CashierId"] as int? ?? 1; 
        }

        private ShiftRevenueData CalculateShiftRevenue(int shiftId)
        {
            var orders = db.Order
               .Include(o => o.Bill)
            .Where(o => o.ShiftId == shiftId)
                 .ToList();

            var totalRevenue = orders
                  .Where(o => o.Bill.Any(b => b.Status == "Paid"))
                .Sum(o => o.Bill.Where(b => b.Status == "Paid").Sum(b => b.FinalAmount));

            var cashRevenue = orders
       .Where(o => o.Bill.Any(b => b.Status == "Paid" && b.PaymentMethod == "cash"))
               .Sum(o => o.Bill.Where(b => b.Status == "Paid" && b.PaymentMethod == "cash").Sum(b => b.FinalAmount));

            return new ShiftRevenueData
            {
                TotalRevenue = totalRevenue,
                CashRevenue = cashRevenue,
                OrderCount = orders.Count(o => o.Bill.Any(b => b.Status == "Paid"))
            };
        }

        private ShiftRevenueReportViewModel GenerateShiftRevenueReportFromDatabase(CashierShift shift)
        {
            var orders = db.Order
        .Include(o => o.Bill)
       .Include(o => o.OrderDetail.Select(od => od.MenuItem))
     .Include(o => o.RestaurantTable)
          .Where(o => o.ShiftId == shift.Id)
          .ToList();

            var paidOrders = orders.Where(o => o.Bill.Any(b => b.Status == "Paid")).ToList();

            var report = new ShiftRevenueReportViewModel
            {
                ShiftId = shift.Id.ToString(),
                CashierName = shift.Employee?.FullName ?? "Thu Ngân",
                ShiftStartTime = shift.StartTime,
                ShiftEndTime = shift.EndTime ?? DateTime.Now,
                OpeningAmount = shift.InitialCash,
                TotalRevenue = shift.TotalRevenue ?? 0,
                TotalOrders = paidOrders.Count,
                CompletedOrders = paidOrders.Count,
                CancelledOrders = orders.Count(o => o.Status == "Cancelled"),
                CashInHand = shift.FinalCash ?? shift.InitialCash,
                RestaurantName = "NHÀ HÀNG LẨU LDP",
                RestaurantAddress = "123 Đường ABC, Quận 1, TP.HCM",
                RestaurantPhone = "0123 456 789",
                RestaurantTaxCode = "0123456789"
            };

            var paymentMethods = paidOrders
             .SelectMany(o => o.Bill.Where(b => b.Status == "Paid"))
               .GroupBy(b => b.PaymentMethod)
            .Select(g => new PaymentMethodSummary
            {
                MethodName = GetPaymentMethodText(g.Key),
                OrderCount = g.Count(),
                TotalAmount = g.Sum(b => b.FinalAmount),
                Percentage = report.TotalRevenue > 0 ? (decimal)(g.Sum(b => b.FinalAmount) / report.TotalRevenue * 100) : 0
            }).ToList();

            report.PaymentMethods = paymentMethods;

            report.TopOrders = paidOrders
                 .OrderByDescending(o => o.Bill.Where(b => b.Status == "Paid").Sum(b => b.FinalAmount))
           .Take(10)
               .Select(o => new OrderSummary
               {
                   OrderId = o.Id.ToString(),
                   TableName = o.RestaurantTable.TableNumber,
                   OrderTime = o.OrderTime,
                   TotalAmount = o.Bill.Where(b => b.Status == "Paid").Sum(b => b.FinalAmount),
                   PaymentMethod = o.Bill.FirstOrDefault()?.PaymentMethod ?? "N/A"
               }).ToList();

            return report;
        }

        private string GetStatusTextVietnamese(string status)
        {
            var statusMap = new Dictionary<string, string>
     {
             { "Pending", "Chờ xử lý" },
               { "Preparing", "Đang chuẩn bị" },
               { "Ready", "Sẵn sàng" },
             { "Completed", "Hoàn thành" },
               { "Cancelled", "Đã hủy" }
  };
            return statusMap.ContainsKey(status) ? statusMap[status] : status;
        }

        private string GetPaymentMethodText(string method)
        {
            var methodMap = new Dictionary<string, string>
  {
                 { "cash", "Tiền mặt" },
                { "card", "Thẻ tín dụng" },
                 { "transfer", "Chuyển khoản" },
                 { "ewallet", "Ví điện tử" }
   };
            return methodMap.ContainsKey(method) ? methodMap[method] : method;
        }

        private JsonResult CheckoutTable(RestaurantTable table)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    if (table.Status != "Occupied")
                    {
                        return Json(new { success = false, message = "Bàn không có khách để checkout!" });
                    }

                    // Kiểm tra xem có order chưa hoàn thành không
                    var incompleteOrder = table.Order
                        .Where(o => o.Status != "Completed" && o.Status != "Cancelled")
                        .FirstOrDefault();

                    if (incompleteOrder != null)
                    {
                        return Json(new { success = false, message = "Bàn còn đơn hàng chưa hoàn thành, không thể checkout!" });
                    }

                    // Cập nhật trạng thái bàn
                    table.Status = "Available";

                    // Cập nhật trạng thái booking nếu có
                    var activeBooking = db.Booking
                        .Where(b => b.TableId == table.Id && (b.Status == "Confirmed" || b.Status == "Pending"))
                        .OrderByDescending(b => b.BookingDateTime)
                        .FirstOrDefault();

                    if (activeBooking != null)
                    {
                        activeBooking.Status = "Completed";
                    }

                    db.SaveChanges();
                    transaction.Commit();

                    return Json(new { success = true, message = $"Bàn {table.TableNumber} đã được trả" });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
                }
            }
        }

        private JsonResult CancelReservation(RestaurantTable table)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    if (table.Status != "Reserved")
                    {
                        return Json(new { success = false, message = "Bàn không ở trạng thái đặt trước!" });
                    }

                    // Cập nhật trạng thái booking khi hủy đặt bàn
                    var activeBooking = db.Booking
                        .Where(b => b.TableId == table.Id && b.Status == "Pending")
                        .OrderByDescending(b => b.BookingDateTime)
                        .FirstOrDefault();

                    if (activeBooking != null)
                    {
                        activeBooking.Status = "Cancelled";
                    }

                    table.Status = "Available";
                    db.SaveChanges();
                    transaction.Commit();

                    return Json(new
                    {
                        success = true,
                        message = $"Đã hủy đặt bàn {table.TableNumber}"
                    });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
                }
            }
        }

        private JsonResult ConfirmReservation(RestaurantTable table)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    if (table.Status != "Reserved")
                    {
                        return Json(new { success = false, message = "Bàn không ở trạng thái đặt trước!" });
                    }

                    // Cập nhật trạng thái booking khi khách đến
                    var activeBooking = db.Booking
                        .Where(b => b.TableId == table.Id && b.Status == "Pending")
                        .OrderByDescending(b => b.BookingDateTime)
                        .FirstOrDefault();

                    if (activeBooking != null)
                    {
                        activeBooking.Status = "Confirmed";
                    }

                    table.Status = "Occupied";
                    db.SaveChanges();
                    transaction.Commit();

                    return Json(new
                    {
                        success = true,
                        message = $"Khách đã đến bàn {table.TableNumber}"
                    });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
                }
            }
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

        #region Chuyển, Gộp, Tách Bàn

        [HttpPost]
        public JsonResult TransferTable(int sourceTableId, int targetTableId)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    if (GetActiveShiftFromDatabase() == null) return Json(new { success = false, message = "Vui lòng mở ca trước!" });

                    var sourceTable = db.RestaurantTable.Include("Order").FirstOrDefault(t => t.Id == sourceTableId);
                    var targetTable = db.RestaurantTable.Include("Order").FirstOrDefault(t => t.Id == targetTableId);

                    if (sourceTable == null || targetTable == null)
                        return Json(new { success = false, message = "Bàn không tồn tại!" });

                    var activeOrder = sourceTable.Order
                        .Where(o => o.Status != "Completed" && o.Status != "Cancelled")
                        .OrderByDescending(o => o.OrderTime)
                        .FirstOrDefault();

                    if (activeOrder == null)
                        return Json(new { success = false, message = "Bàn nguồn không có đơn hàng!" });

                    if (targetTable.Status == "Occupied")
                        return Json(new { success = false, message = "Bàn đích đang có khách. Vui lòng dùng tính năng Gộp bàn!" });

                    activeOrder.TableId = targetTableId;

                    sourceTable.Status = "Available";
                    targetTable.Status = "Occupied";

                    db.SaveChanges();
                    transaction.Commit();

                    return Json(new { success = true, message = "Chuyển bàn thành công!" });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return Json(new { success = false, message = "Lỗi: " + ex.Message });
                }
            }
        }

        [HttpPost]
        public JsonResult MergeTables(int mainTableId, int secondaryTableId)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    if (GetActiveShiftFromDatabase() == null) return Json(new { success = false, message = "Vui lòng mở ca trước!" });

                    var mainTable = db.RestaurantTable.Include("Order.OrderDetail").FirstOrDefault(t => t.Id == mainTableId);
                    var secTable = db.RestaurantTable.Include("Order.OrderDetail").FirstOrDefault(t => t.Id == secondaryTableId);

                    if (mainTable == null || secTable == null) return Json(new { success = false, message = "Bàn không tồn tại!" });

                    var mainOrder = mainTable.Order.FirstOrDefault(o => o.Status != "Completed" && o.Status != "Cancelled");
                    var secOrder = secTable.Order.FirstOrDefault(o => o.Status != "Completed" && o.Status != "Cancelled");

                    if (mainOrder == null || secOrder == null)
                        return Json(new { success = false, message = "Cả hai bàn phải đang có khách mới gộp được!" });

                    foreach (var detail in secOrder.OrderDetail.ToList())
                    {
                        detail.OrderId = mainOrder.Id;
                    }

                    secOrder.Status = "Cancelled";
                    secTable.Status = "Available";

                    db.SaveChanges();
                    transaction.Commit();

                    return Json(new { success = true, message = "Gộp bàn thành công!" });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return Json(new { success = false, message = "Lỗi: " + ex.Message });
                }
            }
        }

        [HttpGet]
        public JsonResult GetTableOrderItems(int tableId)
        {
            try
            {
                var table = db.RestaurantTable.Include("Order.OrderDetail.MenuItem").FirstOrDefault(t => t.Id == tableId);
                if (table == null) return Json(new { success = false, message = "Không tìm thấy bàn" }, JsonRequestBehavior.AllowGet);

                var order = table.Order.FirstOrDefault(o => o.Status != "Completed" && o.Status != "Cancelled");
                if (order == null) return Json(new { success = false, message = "Bàn chưa có đơn hàng" }, JsonRequestBehavior.AllowGet);

                var items = order.OrderDetail.Select(d => new {
                    orderDetailId = d.Id,
                    name = d.MenuItem.Name,
                    quantity = d.Quantity,
                    price = d.PriceAtTime
                }).ToList();

                return Json(new { success = true, items = items }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public JsonResult SplitTable(int sourceTableId, int targetTableId, string itemsToSplit)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                    var items = serializer.Deserialize<List<SplitItemModel>>(itemsToSplit);

                    if (items == null || !items.Any()) return Json(new { success = false, message = "Chưa chọn món để tách!" });

                    var sourceTable = db.RestaurantTable.Include("Order").FirstOrDefault(t => t.Id == sourceTableId);
                    var targetTable = db.RestaurantTable.Include("Order").FirstOrDefault(t => t.Id == targetTableId);

                    var sourceOrder = sourceTable.Order.FirstOrDefault(o => o.Status != "Completed" && o.Status != "Cancelled");
                    if (sourceOrder == null) return Json(new { success = false, message = "Bàn nguồn không có đơn!" });

                    var newOrder = new Order
                    {
                        TableId = targetTableId,
                        OrderTime = DateTime.Now,
                        Status = "Pending",
                        WaiterId = sourceOrder.WaiterId,
                        ShiftId = sourceOrder.ShiftId
                    };
                    db.Order.Add(newOrder);
                    db.SaveChanges();

                    foreach (var item in items)
                    {
                        var originalDetail = db.OrderDetail.Find(item.OrderDetailId);
                        if (originalDetail != null)
                        {
                            if (originalDetail.Quantity == item.Quantity)
                            {
                                originalDetail.OrderId = newOrder.Id;
                            }
                            else if (originalDetail.Quantity > item.Quantity)
                            {
                                originalDetail.Quantity -= item.Quantity;

                                var newDetail = new OrderDetail
                                {
                                    OrderId = newOrder.Id,
                                    MenuItemId = originalDetail.MenuItemId,
                                    Quantity = item.Quantity,
                                    PriceAtTime = originalDetail.PriceAtTime,
                                    Notes = originalDetail.Notes
                                };
                                db.OrderDetail.Add(newDetail);
                            }
                        }
                    }

                    targetTable.Status = "Occupied";

                    db.SaveChanges();
                    transaction.Commit();

                    return Json(new { success = true, message = "Tách bàn thành công!" });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return Json(new { success = false, message = "Lỗi: " + ex.Message });
                }
            }
        }

        public class SplitItemModel
        {
            public int OrderDetailId { get; set; }
            public int Quantity { get; set; }
        }

        #endregion
    }
}