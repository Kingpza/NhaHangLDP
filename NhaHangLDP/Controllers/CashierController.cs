using System;
using System.Text.Json;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using NhaHangLDP.Data.Entities;
using System.Linq;
using System.Threading.Tasks;
using NhaHangLDP.Models;
using NhaHangLDP.Services;
using NhaHangLDP.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace NhaHangLDP.Controllers
{
    public class CashierController : Controller
    {
        private readonly NhaHangLDPEntities db = new NhaHangLDPEntities();
        private readonly ShiftManagementService shiftService;
        private readonly OrderManagementService orderService;
        private readonly PaymentService paymentService;
        private readonly TableManagementService tableService;
        private readonly ReportService reportService;
        private readonly DashboardService dashboardService;
        private readonly OrderQueryService orderQueryService;
        private readonly TableOperationService tableOperationService;
        private readonly EmployeeManagementService employeeService;
        private readonly MenuService menuService;

        public CashierController()
        {
            shiftService = new ShiftManagementService(db);
            orderService = new OrderManagementService(db);
            paymentService = new PaymentService(db);
            tableService = new TableManagementService(db);
            reportService = new ReportService(db);
            dashboardService = new DashboardService(db);
            orderQueryService = new OrderQueryService(db);
            tableOperationService = new TableOperationService(db);
            employeeService = new EmployeeManagementService(db);
            menuService = new MenuService(db);
        }

        #region Shift Management

        public ActionResult OpenShift()
        {
            if (shiftService.GetActiveShift() != null)
                return RedirectToAction("Dashboard");
            return View();
        }

        [HttpPost]
        public ActionResult StartShift(decimal openingAmount, string notes)
        {
            string errorMessage;
            if (shiftService.StartShift(GetCurrentCashierIdFromSession(), openingAmount, notes, out errorMessage))
            {
                var activeShift = shiftService.GetActiveShift();
                HttpContext.Session.SetString("ActiveShiftId", (activeShift.Id).ToString());
                HttpContext.Session.SetString("ShiftStartTime", (activeShift.StartTime).ToString());
                return Json(new { success = true, message = "Mở ca thành công!", shiftId = activeShift.Id, openingAmount });
            }
            return Json(new { success = false, message = errorMessage });
        }

        [HttpPost]
        public ActionResult CloseShift()
        {
            object shiftSummary;
            string errorMessage;

            if (shiftService.CloseShift(out shiftSummary, out errorMessage))
            {
                HttpContext.Session.Remove("ActiveShiftId");
                HttpContext.Session.Remove("ShiftStartTime");
                var summary = (dynamic)shiftSummary;
                return Json(new
                {
                    success = true,
                    message = "Đóng ca thành công!",
                    summary = new { summary.TotalOrders, summary.TotalRevenue, summary.CashInHand },
                    printUrl = Url.Action("PrintShiftRevenueReport", "Cashier", new { shiftId = summary.ShiftId })
                });
            }
            return Json(new { success = false, message = errorMessage });
        }

        public ActionResult PrintShiftRevenueReport(int? shiftId)
        {
            if (!shiftId.HasValue)
                return RedirectToAction("Dashboard");

            var viewModel = reportService.GenerateShiftReport(shiftId.Value);
            if (viewModel == null)
                return RedirectToAction("Dashboard");
            
            return View(viewModel);
        }

        #endregion

        #region Main Pages

        public ActionResult Dashboard()
        {
            var activeShift = db.CashierShift
                .Include(cs => cs.Cashier)
                .Include(s => s.ShiftSupportStaffs)
                    .ThenInclude(ss => ss.Employee)
                .FirstOrDefault(cs => cs.Status == "Active");

            if (activeShift == null)
                return RedirectToAction("OpenShift");

            HttpContext.Session.SetString("ActiveShiftId", (activeShift.Id).ToString());
            HttpContext.Session.SetString("ShiftStartTime", (activeShift.StartTime).ToString());
            HttpContext.Session.SetString("CashierName", (activeShift.Cashier?.FullName ?? "Thu Ngân").ToString());

            return View(dashboardService.GetDashboardData(activeShift.Id));
        }

        public ActionResult Orders()
        {
            if (shiftService.GetActiveShift() == null)
                return RedirectToAction("OpenShift");
            
            return View();
        }

        public ActionResult TableArea()
        {
            if (shiftService.GetActiveShift() == null)
                return RedirectToAction("OpenShift");

            try
            {
                return View(dashboardService.GetTableAreasData());
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi tải danh sách bàn: " + ex.Message;
                return View(new TableAreasViewModel { LastUpdated = DateTime.Now });
            }
        }

        public ActionResult Menu(int? tableId)
        {
            if (shiftService.GetActiveShift() == null)
                return RedirectToAction("OpenShift");
            
            ViewBag.TableId = tableId;
            return View();
        }

        [HttpGet]
        public JsonResult GetMenuItems(string category = "")
        {
            try
            {
                var menuItems = menuService.GetMenuItems(category);
                return Json(new { success = true, items = menuItems });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi tải menu: " + ex.Message });
            }
        }

        public ActionResult SalesPoint()
        {
            if (shiftService.GetActiveShift() == null)
                return RedirectToAction("OpenShift");
            
            return View();
        }

        public ActionResult ShiftDetails(int? shiftId)
        {
            var activeShift = shiftService.GetActiveShift();
            if (activeShift == null)
                return RedirectToAction("OpenShift");

            try
            {
                var viewModel = dashboardService.GetShiftDetails(shiftId ?? activeShift.Id);
                if (viewModel == null)
                {
                    TempData["Error"] = "Không tìm thấy thông tin ca làm việc!";
                    return RedirectToAction("Dashboard");
                }
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
                return NotFound("Không có mã đơn hàng.");

            int numericOrderId;
            if (!int.TryParse(orderId.Replace("DH", ""), out numericOrderId))
            {
                if (!int.TryParse(orderId, out numericOrderId))
                    return NotFound("Mã đơn hàng không hợp lệ: " + orderId);
            }

            try
            {
                var invoiceViewModel = reportService.GenerateInvoice(numericOrderId);
                if (invoiceViewModel == null)
                    return NotFound("Không tìm thấy đơn hàng.");
                
                return View("PrintBill", invoiceViewModel);
            }
            catch (Exception ex)
            {
                return Content("Đã xảy ra lỗi khi tạo hóa đơn: " + ex.Message);
            }
        }

        #endregion

        #region Table Management

        [HttpGet]
        public ActionResult GetTableStatus(int tableId)
        {
            try
            {
                if (shiftService.GetActiveShift() == null)
                    return Json(new { success = false, message = "Vui lòng mở ca trước!" });

                var tableStatus = orderQueryService.GetTableStatus(tableId);
                if (tableStatus == null)
                    return Json(new { success = false, message = "Không tìm thấy bàn!" });
                
                return Json(new { success = true, data = tableStatus });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        [HttpPost]
        public ActionResult UpdateTableStatus(int tableId, string status)
        {
            if (shiftService.GetActiveShift() == null)
                return Json(new { success = false, message = "Vui lòng mở ca trước!" });

            string errorMessage;
            if (tableService.UpdateTableStatus(tableId, status, out errorMessage))
            {
                var table = db.RestaurantTable.Find(tableId);
                return Json(new
                {
                    success = true,
                    message = $"Đã cập nhật trạng thái bàn {table.TableNumber} thành công!",
                    data = new { tableId = table.Id, status }
                });
            }
            return Json(new { success = false, message = errorMessage });
        }

        #endregion

        #region Order Management

        [HttpPost]
        public ActionResult CreateOrder(int tableId, string orderItems, string customerNote)
        {
            var activeShift = shiftService.GetActiveShift();
            if (activeShift == null)
                return Json(new { success = false, message = "Vui lòng mở ca trước khi tạo đơn hàng!" });

            int orderId;
            string errorMessage;
            if (orderService.CreateOrder(tableId, orderItems, customerNote, GetCurrentCashierIdFromSession(), activeShift.Id, out orderId, out errorMessage))
            {
                return Json(new { success = true, message = "Đơn hàng đã được tạo thành công!", orderId });
            }
            return Json(new { success = false, message = errorMessage });
        }

        [HttpPost]
        public ActionResult UpdateOrderStatus(int orderId, string status)
        {
            if (shiftService.GetActiveShift() == null)
                return Json(new { success = false, message = "Vui lòng mở ca trước!" });

            string errorMessage;
            if (orderService.UpdateOrderStatus(orderId, status, out errorMessage))
            {
                return Json(new
                {
                    success = true,
                    message = $"Đã cập nhật trạng thái đơn hàng thành {StatusTextHelper.GetVietnameseStatus(status)}!",
                    data = new { orderId, status }
                });
            }
            return Json(new { success = false, message = errorMessage });
        }

        #endregion

        #region Payment & Invoice

        [HttpPost]
        public ActionResult ProcessPayment(int orderId, string paymentMethod, decimal receivedAmount, decimal totalAmount)
        {
            var activeShift = shiftService.GetActiveShift();
            if (activeShift == null)
                return Json(new { success = false, message = "Vui lòng mở ca trước khi thanh toán!" });

            int billId;
            decimal changeAmount;
            string errorMessage;
            if (paymentService.ProcessPayment(orderId, paymentMethod, receivedAmount, GetCurrentCashierIdFromSession(), activeShift.Id, out billId, out changeAmount, out errorMessage))
            {
                return Json(new { success = true, message = "Thanh toán thành công!", billId, changeAmount });
            }
            return Json(new { success = false, message = errorMessage });
        }

        [HttpPost]
        public ActionResult GenerateInvoice(int orderId)
        {
            try
            {
                if (shiftService.GetActiveShift() == null)
                    return Json(new { success = false, message = "Vui lòng mở ca trước khi tạo hóa đơn!" });

                var order = db.Order
                    .Include(o => o.OrderDetails)
                        .ThenInclude(od => od.MenuItem)
                    .Include(o => o.Table)
                    .Include(o => o.Bills)
                    .FirstOrDefault(o => o.Id == orderId);

                if (order == null)
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng!" });

                var bill = order.Bills.FirstOrDefault();
                var invoiceId = "HD" + DateTime.Now.ToString("yyyyMMddHHmmss");

                return Json(new { success = true, message = "Tạo hóa đơn thành công!", invoiceId, billId = bill?.Id });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        #endregion

        #region Orders Management API

        [HttpGet]
        public JsonResult GetOrdersData(string status = "all")
        {
            try
            {
                var activeShift = shiftService.GetActiveShift();
                if (activeShift == null)
                    return Json(new { success = false, message = "Vui lòng mở ca trước khi xem đơn hàng!" });

                var orders = orderQueryService.GetOrdersData(activeShift.Id, status);
                return Json(new { success = true, orders });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi lấy dữ liệu đơn hàng: " + ex.Message });
            }
        }

        [HttpGet]
        public JsonResult GetOrderDetail(int orderId)
        {
            try
            {
                var orderDetail = orderQueryService.GetOrderDetail(orderId);
                if (orderDetail == null)
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng!" });
                
                return Json(new { success = true, order = orderDetail });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi lấy chi tiết đơn hàng: " + ex.Message });
            }
        }

        [HttpPost]
        public JsonResult UpdateOrderStatusAPI()
        {
            try
            {
                var activeShift = shiftService.GetActiveShift();
                if (activeShift == null)
                    return Json(new { success = false, message = "Vui lòng mở ca trước!" });

                var orderIdStr = Request.Form["orderId"].ToString();
                int orderId;
                if (!int.TryParse(orderIdStr, out orderId))
                    return Json(new { success = false, message = "Mã đơn hàng không hợp lệ!" });

                var status = Request.Form["status"].ToString();
                if (string.IsNullOrEmpty(status))
                    return Json(new { success = false, message = "Thiếu thông tin trạng thái!" });

                status = StatusTextHelper.NormalizeStatus(status);

                string errorMessage;
                if (orderService.UpdateOrderStatus(orderId, status, out errorMessage))
                {
                    return Json(new
                    {
                        success = true,
                        message = $"Đã cập nhật trạng thái đơn hàng thành {StatusTextHelper.GetVietnameseStatus(status)}!",
                        data = new { orderId, status }
                    });
                }
                return Json(new { success = false, message = errorMessage });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi cập nhật trạng thái: " + ex.Message });
            }
        }

        [HttpPost]
        public JsonResult CancelOrderAPI()
        {
            try
            {
                var orderIdStr = Request.Form["orderId"].ToString();
                int orderId;
                if (!int.TryParse(orderIdStr, out orderId))
                    return Json(new { success = false, message = "Mã đơn hàng không hợp lệ!" });

                string errorMessage;
                if (orderService.CancelOrder(orderId, out errorMessage))
                    return Json(new { success = true, message = $"Đơn hàng #{orderId} đã được hủy thành công!" });

                return Json(new { success = false, message = errorMessage });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi hủy đơn hàng: " + ex.Message });
            }
        }

        [HttpPost]
        public JsonResult ProcessPaymentAPI()
        {
            var activeShift = shiftService.GetActiveShift();
            if (activeShift == null)
                return Json(new { success = false, message = "Vui lòng mở ca trước khi thanh toán!" });

            var orderIdStr = Request.Form["orderId"].ToString();
            var paymentMethod = Request.Form["paymentMethod"].ToString();
            var receivedAmountStr = Request.Form["receivedAmount"].ToString();

            if (string.IsNullOrEmpty(orderIdStr) || string.IsNullOrEmpty(paymentMethod) || string.IsNullOrEmpty(receivedAmountStr))
                return Json(new { success = false, message = "Thiếu thông tin thanh toán!" });

            int orderId;
            if (!int.TryParse(orderIdStr, out orderId))
                return Json(new { success = false, message = "Mã đơn hàng không hợp lệ!" });

            decimal receivedAmount;
            if (!decimal.TryParse(receivedAmountStr, out receivedAmount))
                return Json(new { success = false, message = "Số tiền nhận không hợp lệ!" });

            int billId;
            decimal changeAmount;
            string errorMessage;
            if (paymentService.ProcessPayment(orderId, paymentMethod, receivedAmount, GetCurrentCashierIdFromSession(), activeShift.Id, out billId, out changeAmount, out errorMessage))
            {
                return Json(new
                {
                    success = true,
                    message = "Thanh toán thành công!",
                    billId,
                    changeAmount,
                    finalAmount = receivedAmount - changeAmount
                });
            }
            return Json(new { success = false, message = errorMessage });
        }

        #endregion

        #region Dashboard & Statistics APIs

        [HttpGet]
        public JsonResult GetLastPaidOrder()
        {
            try
            {
                var activeShift = shiftService.GetActiveShift();
                if (activeShift == null)
                    return Json(new { success = false, message = "Ca làm việc không hoạt động." });

                var orderId = orderQueryService.GetLastPaidOrderId(activeShift.Id);
                if (orderId.HasValue)
                    return Json(new { success = true, orderId = orderId.Value.ToString() });
                
                return Json(new { success = false, message = "Không tìm thấy hóa đơn nào đã thanh toán trong ca này." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi máy chủ: " + ex.Message });
            }
        }

        [HttpGet]
        public JsonResult GetDashboardStats()
        {
            try
            {
                var activeShift = shiftService.GetActiveShift();
                if (activeShift == null)
                    return Json(new { success = false, message = "Ca làm việc không hoạt động." });

                var stats = dashboardService.GetDashboardStats(activeShift.Id);
                return Json(new { success = true, data = stats });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi máy chủ: " + ex.Message });
            }
        }

        #endregion

        #region Table Management APIs

        [HttpGet]
        public JsonResult GetTablesData()
        {
            try
            {
                if (shiftService.GetActiveShift() == null)
                    return Json(new { success = false, message = "Vui lòng mở ca trước!" });

                var tablesData = orderQueryService.GetTablesData();
                return Json(new { success = true, tables = tablesData });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi tải dữ liệu bàn: " + ex.Message });
            }
        }

        [HttpPost]
        public JsonResult UpdateTableInfo()
        {
            if (shiftService.GetActiveShift() == null)
                return Json(new { success = false, message = "Vui lòng mở ca trước!" });

            var tableIdStr = Request.Form["tableId"].ToString();
            var customersStr = Request.Form["customers"].ToString();
            
            int tableId;
            int customers;
            
            if (!int.TryParse(tableIdStr, out tableId))
                return Json(new { success = false, message = "Mã bàn không hợp lệ!" });
                
            if (!int.TryParse(customersStr, out customers) || customers <= 0)
                return Json(new { success = false, message = "Số khách không hợp lệ!" });

            var action = Request.Form["action"].ToString();
            var customerName = Request.Form["customerName"].ToString() ?? "";
            var customerPhone = Request.Form["customerPhone"].ToString() ?? "";
            var notes = Request.Form["notes"].ToString() ?? "";
            var time = Request.Form["time"].ToString() ?? "";

            bool success = false;
            string message = "";
            string errorMessage;

            if (action == "assign")
            {
                success = tableService.AssignTable(tableId, customers, customerName, 
                    customerPhone, notes, out errorMessage);
                if (success)
                {
                    var table = db.RestaurantTable.Find(tableId);
                    message = $"Đã xếp {customers} khách vào bàn {table.TableNumber}";
                }
            }
            else if (action == "reserve")
            {
                success = tableService.ReserveTable(tableId, customers, customerName, 
                    customerPhone, notes, time, out errorMessage);
                if (success)
                {
                    var table = db.RestaurantTable.Find(tableId);
                    message = $"Đã đặt bàn {table.TableNumber} cho {customers} khách lúc {time}";
                }
            }
            else
            {
                return Json(new { success = false, message = "Hành động không hợp lệ!" });
            }

            if (success)
                return Json(new { success = true, message });
            
            return Json(new { success = false, message = errorMessage });
        }

        [HttpPost]
        public JsonResult ConfirmReservationAPI()
        {
            var tableIdStr = Request.Form["tableId"].ToString();
            int tableId;
            if (!int.TryParse(tableIdStr, out tableId))
                return Json(new { success = false, message = "Mã bàn không hợp lệ!" });

            string errorMessage;
            if (tableService.ConfirmReservation(tableId, out errorMessage))
            {
                var table = db.RestaurantTable.Find(tableId);
                return Json(new { success = true, message = $"Khách đã đến bàn {table.TableNumber}" });
            }
            return Json(new { success = false, message = errorMessage });
        }

        [HttpPost]
        public JsonResult CancelReservationAPI()
        {
            var tableIdStr = Request.Form["tableId"].ToString();
            int tableId;
            if (!int.TryParse(tableIdStr, out tableId))
                return Json(new { success = false, message = "Mã bàn không hợp lệ!" });

            string errorMessage;
            if (tableService.CancelReservation(tableId, out errorMessage))
            {
                var table = db.RestaurantTable.Find(tableId);
                return Json(new { success = true, message = $"Đã hủy đặt bàn {table.TableNumber}" });
            }
            return Json(new { success = false, message = errorMessage });
        }

        [HttpPost]
        public JsonResult CheckoutTableAPI()
        {
            var tableIdStr = Request.Form["tableId"].ToString();
            int tableId;
            if (!int.TryParse(tableIdStr, out tableId))
                return Json(new { success = false, message = "Mã bàn không hợp lệ!" });

            string errorMessage;
            if (tableService.CheckoutTable(tableId, out errorMessage))
            {
                var table = db.RestaurantTable.Find(tableId);
                return Json(new { success = true, message = $"Bàn {table.TableNumber} đã được trả" });
            }
            return Json(new { success = false, message = errorMessage });
        }

        #endregion

        #region Support Staff Management

        [HttpGet]
        public JsonResult GetAvailableEmployees()
        {
            try
            {
                var activeShift = db.CashierShift.FirstOrDefault(s => s.Status == "Active");
                if (activeShift == null)
                    return Json(new List<object>());

                var employees = employeeService.GetAvailableEmployees(activeShift.CashierId);
                return Json(employees);
            }
            catch (Exception ex)
            {
                Response.StatusCode = 500;
                return Json(new { message = "Lỗi máy chủ: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<JsonResult> RemoveSupportEmployee(int shiftId, int employeeId)
        {
            var result = await employeeService.RemoveSupportEmployee(shiftId, employeeId);
            if (result.Success)
                return Json(new { success = true });
            
            return Json(new { success = false, message = result.ErrorMessage });
        }

        [HttpPost]
        public async Task<JsonResult> AddSupportEmployee(int shiftId, int employeeId)
        {
            var result = await employeeService.AddSupportEmployee(shiftId, employeeId);
            if (result.Success)
                return Json(new { success = true });
            
            return Json(new { success = false, message = result.ErrorMessage });
        }

        [HttpPost]
        public async Task<JsonResult> HandoverShift(int shiftId, int newEmployeeId)
        {
            var result = await employeeService.HandoverShift(shiftId, newEmployeeId);
            if (result.Success)
                return Json(new { success = true });
            
            return Json(new { success = false, message = result.ErrorMessage });
        }

        #endregion

        #region Table Operations

        [HttpPost]
        public JsonResult TransferTable(int sourceTableId, int targetTableId)
        {
            if (shiftService.GetActiveShift() == null)
                return Json(new { success = false, message = "Vui lòng mở ca trước!" });

            string errorMessage;
            if (tableService.TransferTable(sourceTableId, targetTableId, out errorMessage))
                return Json(new { success = true, message = "Chuyển bàn thành công!" });
            
            return Json(new { success = false, message = errorMessage });
        }

        [HttpPost]
        public JsonResult MergeTables(int mainTableId, int secondaryTableId)
        {
            if (shiftService.GetActiveShift() == null)
                return Json(new { success = false, message = "Vui lòng mở ca trước!" });

            string errorMessage;
            if (tableService.MergeTables(mainTableId, secondaryTableId, out errorMessage))
                return Json(new { success = true, message = "Gộp bàn thành công!" });
            
            return Json(new { success = false, message = errorMessage });
        }

        [HttpGet]
        public JsonResult GetTableOrderItems(int tableId)
        {
            try
            {
                var items = orderQueryService.GetTableOrderItems(tableId);
                if (items == null)
                    return Json(new { success = false, message = "Bàn chưa có đơn hàng" });
                
                return Json(new { success = true, items });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult SplitTable(int sourceTableId, int targetTableId, string itemsToSplit)
        {
            try
            {
                var items = JsonSerializer.Deserialize<List<TableOperationService.SplitItemModel>>(itemsToSplit);

                string errorMessage;
                if (tableOperationService.SplitTable(sourceTableId, targetTableId, items, out errorMessage))
                    return Json(new { success = true, message = "Tách bàn thành công!" });
                
                return Json(new { success = false, message = errorMessage });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        #endregion

        #region Helper Methods

        private int GetCurrentCashierIdFromSession()
        {
            return HttpContext.Session.GetInt32("CashierId") ?? 1;
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

        #region Online Orders Management

        /// <summary>
        /// Trang quản lý đơn hàng online
        /// </summary>
        public ActionResult OnlineOrders(string status = "all", string orderType = "all")
        {
            if (shiftService.GetActiveShift() == null)
                return RedirectToAction("OpenShift");

            ViewBag.StatusCounts = orderQueryService.GetOnlineOrderCounts();
            ViewBag.SelectedStatus = status;
            ViewBag.SelectedOrderType = orderType;

            var orders = orderQueryService.GetOnlineOrders(status, orderType);
            return View(orders);
        }

        /// <summary>
        /// API lấy danh sách đơn hàng online
        /// </summary>
        [HttpGet]
        public JsonResult GetOnlineOrdersData(string status = "all", string orderType = "all")
        {
            try
            {
                if (shiftService.GetActiveShift() == null)
                    return Json(new { success = false, message = "Vui lòng mở ca trước!" });

                var orders = orderQueryService.GetOnlineOrders(status, orderType);
                var counts = orderQueryService.GetOnlineOrderCounts();

                return Json(new { success = true, orders, counts });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// API lấy chi tiết đơn hàng online
        /// </summary>
        [HttpGet]
        public JsonResult GetOnlineOrderDetail(int orderId)
        {
            try
            {
                var order = orderQueryService.GetOnlineOrderDetail(orderId);
                if (order == null)
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng!" });

                return Json(new { success = true, order });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// Xác nhận đơn hàng online
        /// </summary>
        [HttpPost]
        public JsonResult ConfirmOnlineOrder(int orderId)
        {
            try
            {
                if (shiftService.GetActiveShift() == null)
                    return Json(new { success = false, message = "Vui lòng mở ca trước!" });

                string errorMessage;
                if (orderQueryService.UpdateOnlineOrderStatus(orderId, "Confirmed", out errorMessage))
                {
                    return Json(new { success = true, message = "Đã xác nhận đơn hàng!" });
                }
                return Json(new { success = false, message = errorMessage });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// Bắt đầu chuẩn bị đơn hàng online
        /// </summary>
        [HttpPost]
        public JsonResult StartPreparingOnlineOrder(int orderId)
        {
            try
            {
                if (shiftService.GetActiveShift() == null)
                    return Json(new { success = false, message = "Vui lòng mở ca trước!" });

                string errorMessage;
                if (orderQueryService.UpdateOnlineOrderStatus(orderId, "Preparing", out errorMessage))
                {
                    return Json(new { success = true, message = "Đã bắt đầu chuẩn bị!" });
                }
                return Json(new { success = false, message = errorMessage });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// Đánh dấu đơn hàng sẵn sàng
        /// </summary>
        [HttpPost]
        public JsonResult MarkOnlineOrderReady(int orderId)
        {
            try
            {
                if (shiftService.GetActiveShift() == null)
                    return Json(new { success = false, message = "Vui lòng mở ca trước!" });

                string errorMessage;
                if (orderQueryService.UpdateOnlineOrderStatus(orderId, "Ready", out errorMessage))
                {
                    return Json(new { success = true, message = "Đơn hàng đã sẵn sàng!" });
                }
                return Json(new { success = false, message = errorMessage });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// Hoàn thành đơn hàng online (cho đơn tự đến lấy hoặc không giao hàng)
        /// </summary>
        [HttpPost]
        public JsonResult CompleteOnlineOrder(int orderId)
        {
            try
            {
                if (shiftService.GetActiveShift() == null)
                    return Json(new { success = false, message = "Vui lòng mở ca trước!" });

                var order = db.CustomerOrder.Find(orderId);
                if (order == null)
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng!" });

                // Log để debug
                System.Diagnostics.Debug.WriteLine($"CompleteOnlineOrder - OrderId: {orderId}, OrderType: '{order.OrderType}', Status: '{order.Status}'");

                // Cho phép hoàn thành đơn không phải Delivery khi đã Ready
                // OrderType có thể là "Pickup", "pickup", "TakeAway", "DineIn" hoặc bất kỳ giá trị nào không phải "Delivery"
                bool isNotDelivery = string.IsNullOrEmpty(order.OrderType) || 
                                     !order.OrderType.Equals("Delivery", StringComparison.OrdinalIgnoreCase);
                
                if (isNotDelivery && order.Status == "Ready")
                {
                    order.Status = "Completed";
                    order.CompletedDate = DateTime.Now;
                    order.PaymentStatus = "Paid";
                    db.SaveChanges();
                    return Json(new { success = true, message = "Đã hoàn thành đơn hàng!" });
                }

                // Nếu đơn đang Delivering và cần hoàn thành thủ công
                if (order.Status == "Delivering")
                {
                    order.Status = "Completed";
                    order.CompletedDate = DateTime.Now;
                    order.PaymentStatus = "Paid";

                    // Cập nhật shipper status
                    var assignment = db.DeliveryAssignment
                        .FirstOrDefault(a => a.OrderId == orderId && (a.Status == "Assigned" || a.Status == "PickedUp"));
                    if (assignment != null)
                    {
                        assignment.Status = "Delivered";
                        var shipper = db.Shipper.Find(assignment.ShipperId);
                        if (shipper != null)
                        {
                            shipper.Status = "Available";
                            shipper.TotalDeliveries = shipper.TotalDeliveries + 1;
                        }
                    }

                    db.SaveChanges();
                    return Json(new { success = true, message = "Đã hoàn thành đơn hàng!" });
                }

                return Json(new { 
                    success = false, 
                    message = $"Không thể hoàn thành đơn hàng này! (OrderType: {order.OrderType}, Status: {order.Status})" 
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// Hủy đơn hàng online
        /// </summary>
        [HttpPost]
        public JsonResult CancelOnlineOrder(int orderId, string reason)
        {
            try
            {
                if (shiftService.GetActiveShift() == null)
                    return Json(new { success = false, message = "Vui lòng mở ca trước!" });

                var order = db.CustomerOrder.Find(orderId);
                if (order == null)
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng!" });

                // Không cho hủy đơn đang giao hoặc đã hoàn thành
                if (order.Status == "Delivering" || order.Status == "Completed")
                    return Json(new { success = false, message = "Không thể hủy đơn hàng này!" });

                order.Status = "Cancelled";
                order.CancelledDate = DateTime.Now;
                order.CancelReason = reason;

                // Hủy assignment nếu có
                var assignment = db.DeliveryAssignment
                    .FirstOrDefault(a => a.OrderId == orderId && a.Status != "Cancelled" && a.Status != "Delivered");
                if (assignment != null)
                {
                    assignment.Status = "Cancelled";
                    var shipper = db.Shipper.Find(assignment.ShipperId);
                    if (shipper != null) shipper.Status = "Available";
                }

                db.SaveChanges();
                return Json(new { success = true, message = "Đã hủy đơn hàng!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// Gán shipper cho đơn hàng online
        /// </summary>
        [HttpPost]
        public JsonResult AssignShipperToOnlineOrder(int orderId, int shipperId)
        {
            try
            {
                if (shiftService.GetActiveShift() == null)
                    return Json(new { success = false, message = "Vui lòng mở ca trước!" });

                var order = db.CustomerOrder.Find(orderId);
                if (order == null)
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng!" });

                if (order.OrderType != "Delivery")
                    return Json(new { success = false, message = "Đơn hàng này không phải đơn giao hàng!" });

                if (order.Status != "Ready")
                    return Json(new { success = false, message = "Đơn hàng chưa sẵn sàng để giao!" });

                var shipper = db.Shipper.Find(shipperId);
                if (shipper == null || !shipper.IsActive)
                    return Json(new { success = false, message = "Shipper không hợp lệ!" });

                if (shipper.Status == "Busy")
                    return Json(new { success = false, message = "Shipper đang bận!" });

                // Tạo assignment
                var assignment = new DeliveryAssignment
                {
                    OrderId = orderId,
                    ShipperId = shipperId,
                    AssignedTime = DateTime.Now,
                    Status = "Assigned",
                    DeliveryFee = order.DeliveryFee,
                    ShipperEarning = order.DeliveryFee * 0.8m // 80% phí ship
                };

                db.DeliveryAssignment.Add(assignment);
                shipper.Status = "Busy";
                order.Status = "Delivering";
                order.DeliveringDate = DateTime.Now;

                db.SaveChanges();

                return Json(new { success = true, message = $"Đã gán shipper {shipper.FullName} cho đơn hàng!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// Lấy danh sách shipper khả dụng
        /// </summary>
        [HttpGet]
        public JsonResult GetAvailableShippersForOrder()
        {
            try
            {
                var shippers = db.Shipper
                    .Where(s => s.IsActive && s.Status == "Available")
                    .Select(s => new
                    {
                        s.Id,
                        s.FullName,
                        s.Phone,
                        s.VehicleType,
                        s.Rating,
                        s.TotalDeliveries
                    })
                    .OrderByDescending(s => s.Rating)
                    .ToList();

                return Json(new { success = true, shippers });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region Reservation Management

        /// <summary>
        /// Trang quản lý đơn đặt bàn
        /// </summary>
        public ActionResult Reservations(string status = "all", string date = "")
        {
            if (shiftService.GetActiveShift() == null)
                return RedirectToAction("OpenShift");

            ViewBag.SelectedStatus = status;
            ViewBag.SelectedDate = date;
            ViewBag.StatusCounts = GetReservationCounts();

            return View();
        }

        /// <summary>
        /// API lấy danh sách đơn đặt bàn
        /// </summary>
        [HttpGet]
        public JsonResult GetReservationsData(string status = "all", string date = "")
        {
            try
            {
                if (shiftService.GetActiveShift() == null)
                    return Json(new { success = false, message = "Vui lòng mở ca trước!" });

                var query = db.Reservation.AsQueryable();

                // Filter by status
                if (!string.IsNullOrEmpty(status) && status != "all")
                {
                    query = query.Where(r => r.Status == status);
                }

                // Filter by date
                DateTime filterDate;
                if (!string.IsNullOrEmpty(date) && DateTime.TryParse(date, out filterDate))
                {
                    var filterDateOnly = DateOnly.FromDateTime(filterDate);
                    query = query.Where(r => r.ReservationDate == filterDateOnly);
                }
                else
                {
                    // Default: show today and future reservations
                    var todayOnly = DateOnly.FromDateTime(DateTime.Today);
                    query = query.Where(r => r.ReservationDate >= todayOnly);
                }

                var reservations = query
                    .OrderBy(r => r.ReservationDate)
                    .ThenBy(r => r.ReservationTime)
                    .ToList()
                    .Select(r => new
                    {
                        r.Id,
                        r.ReservationCode,
                        r.CustomerName,
                        r.CustomerPhone,
                        r.CustomerEmail,
                        ReservationDate = r.ReservationDate.ToString("dd/MM/yyyy"),
                        ReservationTime = r.ReservationTime.ToString(@"hh\:mm"),
                        r.NumberOfGuests,
                        r.TablePreference,
                        r.SpecialRequests,
                        r.Status,
                        StatusText = GetReservationStatusText(r.Status),
                        StatusClass = GetReservationStatusClass(r.Status),
                        CreatedDate = r.CreatedDate.ToString("dd/MM/yyyy HH:mm"),
                        CanConfirm = r.Status == "Pending",
                        CanComplete = r.Status == "Confirmed",
                        CanCancel = r.Status == "Pending" || r.Status == "Confirmed"
                    })
                    .ToList();

                var counts = GetReservationCounts();

                return Json(new { success = true, reservations, counts });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// API lấy chi tiết đơn đặt bàn
        /// </summary>
        [HttpGet]
        public JsonResult GetReservationDetail(int id)
        {
            try
            {
                var reservation = db.Reservation
                    .Where(r => r.Id == id)
                    .Select(r => new
                    {
                        r.Id,
                        r.ReservationCode,
                        r.CustomerName,
                        r.CustomerPhone,
                        r.CustomerEmail,
                        ReservationDate = r.ReservationDate,
                        ReservationTime = r.ReservationTime,
                        r.NumberOfGuests,
                        r.TableId,
                        r.TablePreference,
                        r.SpecialRequests,
                        r.Status,
                        r.DepositAmount,
                        r.DepositPaid,
                        r.CancelReason,
                        r.CreatedDate,
                        r.ConfirmedDate,
                        r.CancelledDate
                    })
                    .FirstOrDefault();

                if (reservation == null)
                    return Json(new { success = false, message = "Không tìm thấy đơn đặt bàn!" });

                // Get table info if assigned
                string tableName = null;
                if (reservation.TableId.HasValue)
                {
                    var table = db.RestaurantTable.Find(reservation.TableId.Value);
                    tableName = table?.TableNumber;
                }

                return Json(new
                {
                    success = true,
                    reservation = new
                    {
                        reservation.Id,
                        reservation.ReservationCode,
                        reservation.CustomerName,
                        reservation.CustomerPhone,
                        reservation.CustomerEmail,
                        ReservationDate = reservation.ReservationDate.ToString("dd/MM/yyyy"),
                        ReservationTime = reservation.ReservationTime.ToString(@"hh\:mm"),
                        reservation.NumberOfGuests,
                        reservation.TableId,
                        TableName = tableName,
                        reservation.TablePreference,
                        reservation.SpecialRequests,
                        reservation.Status,
                        StatusText = GetReservationStatusText(reservation.Status),
                        StatusClass = GetReservationStatusClass(reservation.Status),
                        reservation.DepositAmount,
                        reservation.DepositPaid,
                        reservation.CancelReason,
                        CreatedDate = reservation.CreatedDate.ToString("dd/MM/yyyy HH:mm"),
                        ConfirmedDate = reservation.ConfirmedDate?.ToString("dd/MM/yyyy HH:mm"),
                        CancelledDate = reservation.CancelledDate?.ToString("dd/MM/yyyy HH:mm"),
                        CanConfirm = reservation.Status == "Pending",
                        CanComplete = reservation.Status == "Confirmed",
                        CanCancel = reservation.Status == "Pending" || reservation.Status == "Confirmed"
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// Xác nhận đơn đặt bàn
        /// </summary>
        [HttpPost]
        public JsonResult ConfirmReservation(int id, int? tableId)
        {
            try
            {
                if (shiftService.GetActiveShift() == null)
                    return Json(new { success = false, message = "Vui lòng mở ca trước!" });

                var reservation = db.Reservation.Find(id);
                if (reservation == null)
                    return Json(new { success = false, message = "Không tìm thấy đơn đặt bàn!" });

                if (reservation.Status != "Pending")
                    return Json(new { success = false, message = "Đơn đặt bàn không ở trạng thái chờ xác nhận!" });

                reservation.Status = "Confirmed";
                reservation.ConfirmedDate = DateTime.Now;

                if (tableId.HasValue && tableId.Value > 0)
                {
                    reservation.TableId = tableId.Value;
                }

                db.SaveChanges();

                return Json(new { success = true, message = "Đã xác nhận đơn đặt bàn!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// Từ chối đơn đặt bàn
        /// </summary>
        [HttpPost]
        public JsonResult RejectReservation(int id, string reason)
        {
            try
            {
                if (shiftService.GetActiveShift() == null)
                    return Json(new { success = false, message = "Vui lòng mở ca trước!" });

                var reservation = db.Reservation.Find(id);
                if (reservation == null)
                    return Json(new { success = false, message = "Không tìm thấy đơn đặt bàn!" });

                if (reservation.Status != "Pending" && reservation.Status != "Confirmed")
                    return Json(new { success = false, message = "Không thể từ chối đơn đặt bàn này!" });

                reservation.Status = "Cancelled";
                reservation.CancelReason = reason ?? "Từ chối bởi nhà hàng";
                reservation.CancelledDate = DateTime.Now;

                db.SaveChanges();

                return Json(new { success = true, message = "Đã từ chối đơn đặt bàn!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// Hoàn thành đơn đặt bàn (khách đã đến)
        /// </summary>
        [HttpPost]
        public JsonResult CompleteReservation(int id)
        {
            try
            {
                if (shiftService.GetActiveShift() == null)
                    return Json(new { success = false, message = "Vui lòng mở ca trước!" });

                var reservation = db.Reservation.Find(id);
                if (reservation == null)
                    return Json(new { success = false, message = "Không tìm thấy đơn đặt bàn!" });

                if (reservation.Status != "Confirmed")
                    return Json(new { success = false, message = "Đơn đặt bàn chưa được xác nhận!" });

                reservation.Status = "Completed";

                // Update table status if assigned
                if (reservation.TableId.HasValue)
                {
                    var table = db.RestaurantTable.Find(reservation.TableId.Value);
                    if (table != null)
                    {
                        table.Status = "Occupied";
                    }
                }

                db.SaveChanges();

                return Json(new { success = true, message = "Khách đã đến! Đơn đặt bàn hoàn thành." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// Đánh dấu khách không đến
        /// </summary>
        [HttpPost]
        public JsonResult NoShowReservation(int id)
        {
            try
            {
                if (shiftService.GetActiveShift() == null)
                    return Json(new { success = false, message = "Vui lòng mở ca trước!" });

                var reservation = db.Reservation.Find(id);
                if (reservation == null)
                    return Json(new { success = false, message = "Không tìm thấy đơn đặt bàn!" });

                if (reservation.Status != "Confirmed")
                    return Json(new { success = false, message = "Đơn đặt bàn chưa được xác nhận!" });

                reservation.Status = "NoShow";
                reservation.CancelReason = "Khách không đến";
                reservation.CancelledDate = DateTime.Now;

                db.SaveChanges();

                return Json(new { success = true, message = "Đã đánh dấu khách không đến." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// Lấy danh sách bàn trống cho đặt bàn
        /// </summary>
        [HttpGet]
        public JsonResult GetAvailableTablesForReservation(int reservationId)
        {
            try
            {
                var reservation = db.Reservation.Find(reservationId);
                if (reservation == null)
                    return Json(new { success = false, message = "Không tìm thấy đơn đặt bàn!" });

                var timeMinutes = (int)reservation.ReservationTime.ToTimeSpan().TotalMinutes;

                // Get tables that can accommodate guests and are not reserved at the same time
                var tables = db.RestaurantTable
                    .Where(t => t.Capacity >= reservation.NumberOfGuests)
                    .ToList()
                    .Select(t => new
                    {
                        t.Id,
                        t.TableNumber,
                        t.Capacity,
                        t.Status,
                        IsAvailable = !db.Reservation.Any(r =>
                            r.Id != reservationId &&
                            r.TableId == t.Id &&
                            r.ReservationDate == reservation.ReservationDate &&
                            r.Status != "Cancelled" && r.Status != "NoShow" && r.Status != "Completed" &&
                            Math.Abs((int)r.ReservationTime.ToTimeSpan().TotalMinutes - timeMinutes) < 120)
                    })
                    .OrderBy(t => t.Capacity)
                    .ThenBy(t => t.TableNumber)
                    .ToList();

                return Json(new { success = true, tables });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// Lấy số lượng đơn đặt bàn theo trạng thái
        /// </summary>
        private object GetReservationCounts()
        {
            var todayOnly = DateOnly.FromDateTime(DateTime.Today);
            var reservations = db.Reservation.Where(r => r.ReservationDate >= todayOnly).ToList();

            return new
            {
                all = reservations.Count,
                pending = reservations.Count(r => r.Status == "Pending"),
                confirmed = reservations.Count(r => r.Status == "Confirmed"),
                completed = reservations.Count(r => r.Status == "Completed"),
                cancelled = reservations.Count(r => r.Status == "Cancelled" || r.Status == "NoShow")
            };
        }

        private string GetReservationStatusText(string status)
        {
            switch (status)
            {
                case "Pending": return "Chờ xác nhận";
                case "Confirmed": return "Đã xác nhận";
                case "Completed": return "Hoàn thành";
                case "Cancelled": return "Đã hủy";
                case "NoShow": return "Không đến";
                default: return status;
            }
        }

        private string GetReservationStatusClass(string status)
        {
            switch (status)
            {
                case "Pending": return "warning";
                case "Confirmed": return "success";
                case "Completed": return "info";
                case "Cancelled": return "danger";
                case "NoShow": return "secondary";
                default: return "secondary";
            }
        }

        #endregion
    }
}