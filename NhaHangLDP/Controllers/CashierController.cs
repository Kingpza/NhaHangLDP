using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using NhaHangLDP.Models;
using NhaHangLDP.Services;
using NhaHangLDP.Helpers;

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
        private readonly RequestHandlerService requestHandler;

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
            requestHandler = new RequestHandlerService();
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
                Session["ActiveShiftId"] = activeShift.Id;
                Session["ShiftStartTime"] = activeShift.StartTime;
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
                Session.Remove("ActiveShiftId");
                Session.Remove("ShiftStartTime");
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
                .Include(cs => cs.Employee)
                .Include(s => s.ShiftSupportStaff.Select(ss => ss.Employee))
                .FirstOrDefault(cs => cs.Status == "Active");

            if (activeShift == null)
                return RedirectToAction("OpenShift");

            Session["ActiveShiftId"] = activeShift.Id;
            Session["ShiftStartTime"] = activeShift.StartTime;
            Session["CashierName"] = activeShift.Employee?.FullName ?? "Thu Ngân";

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
                return Json(new { success = true, items = menuItems }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi tải menu: " + ex.Message }, JsonRequestBehavior.AllowGet);
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
                return HttpNotFound("Không có mã đơn hàng.");

            int numericOrderId;
            if (!int.TryParse(orderId.Replace("DH", ""), out numericOrderId))
            {
                if (!int.TryParse(orderId, out numericOrderId))
                    return HttpNotFound("Mã đơn hàng không hợp lệ: " + orderId);
            }

            try
            {
                var invoiceViewModel = reportService.GenerateInvoice(numericOrderId);
                if (invoiceViewModel == null)
                    return HttpNotFound("Không tìm thấy đơn hàng.");
                
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
                    return Json(new { success = false, message = "Vui lòng mở ca trước!" }, JsonRequestBehavior.AllowGet);

                var tableStatus = orderQueryService.GetTableStatus(tableId);
                if (tableStatus == null)
                    return Json(new { success = false, message = "Không tìm thấy bàn!" }, JsonRequestBehavior.AllowGet);
                
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
                    .Include(o => o.OrderDetail.Select(od => od.MenuItem))
                    .Include(o => o.RestaurantTable)
                    .Include(o => o.Bill)
                    .FirstOrDefault(o => o.Id == orderId);

                if (order == null)
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng!" });

                var bill = order.Bill.FirstOrDefault();
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
                    return Json(new { success = false, message = "Vui lòng mở ca trước khi xem đơn hàng!" }, JsonRequestBehavior.AllowGet);

                var orders = orderQueryService.GetOrdersData(activeShift.Id, status);
                return Json(new { success = true, orders }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi lấy dữ liệu đơn hàng: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult GetOrderDetail(int orderId)
        {
            try
            {
                var orderDetail = orderQueryService.GetOrderDetail(orderId);
                if (orderDetail == null)
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng!" }, JsonRequestBehavior.AllowGet);
                
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
                var activeShift = shiftService.GetActiveShift();
                if (activeShift == null)
                    return Json(new { success = false, message = "Vui lòng mở ca trước!" });

                int orderId;
                string errorMessage;
                if (!requestHandler.TryParseOrderId(Request, out orderId, out errorMessage))
                    return Json(new { success = false, message = errorMessage });

                var status = Request.Form["status"];
                if (string.IsNullOrEmpty(status))
                    return Json(new { success = false, message = "Thiếu thông tin trạng thái!" });

                status = StatusTextHelper.NormalizeStatus(status);

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
                int orderId;
                string errorMessage;
                if (!requestHandler.TryParseOrderId(Request, out orderId, out errorMessage))
                    return Json(new { success = false, message = errorMessage });

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

            int orderId;
            string paymentMethod;
            decimal receivedAmount;
            string errorMessage;

            if (!requestHandler.TryParsePaymentRequest(Request, out orderId, out paymentMethod, out receivedAmount, out errorMessage))
                return Json(new { success = false, message = errorMessage });

            int billId;
            decimal changeAmount;
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
                    return Json(new { success = false, message = "Ca làm việc không hoạt động." }, JsonRequestBehavior.AllowGet);

                var orderId = orderQueryService.GetLastPaidOrderId(activeShift.Id);
                if (orderId.HasValue)
                    return Json(new { success = true, orderId = orderId.Value.ToString() }, JsonRequestBehavior.AllowGet);
                
                return Json(new { success = false, message = "Không tìm thấy hóa đơn nào đã thanh toán trong ca này." }, JsonRequestBehavior.AllowGet);
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
                var activeShift = shiftService.GetActiveShift();
                if (activeShift == null)
                    return Json(new { success = false, message = "Ca làm việc không hoạt động." }, JsonRequestBehavior.AllowGet);

                var stats = dashboardService.GetDashboardStats(activeShift.Id);
                return Json(new { success = true, data = stats }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi máy chủ: " + ex.Message }, JsonRequestBehavior.AllowGet);
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
                    return Json(new { success = false, message = "Vui lòng mở ca trước!" }, JsonRequestBehavior.AllowGet);

                var tablesData = orderQueryService.GetTablesData();
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
            if (shiftService.GetActiveShift() == null)
                return Json(new { success = false, message = "Vui lòng mở ca trước!" });

            RequestHandlerService.TableInfoRequest tableInfo;
            string errorMessage;
            if (!requestHandler.TryParseTableInfoRequest(Request, out tableInfo, out errorMessage))
                return Json(new { success = false, message = errorMessage });

            bool success = false;
            string message = "";

            if (tableInfo.Action == "assign")
            {
                success = tableService.AssignTable(tableInfo.TableId, tableInfo.Customers, tableInfo.CustomerName, 
                    tableInfo.CustomerPhone, tableInfo.Notes, out errorMessage);
                if (success)
                {
                    var table = db.RestaurantTable.Find(tableInfo.TableId);
                    message = $"Đã xếp {tableInfo.Customers} khách vào bàn {table.TableNumber}";
                }
            }
            else if (tableInfo.Action == "reserve")
            {
                success = tableService.ReserveTable(tableInfo.TableId, tableInfo.Customers, tableInfo.CustomerName, 
                    tableInfo.CustomerPhone, tableInfo.Notes, tableInfo.Time, out errorMessage);
                if (success)
                {
                    var table = db.RestaurantTable.Find(tableInfo.TableId);
                    message = $"Đã đặt bàn {table.TableNumber} cho {tableInfo.Customers} khách lúc {tableInfo.Time}";
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
            int tableId;
            string errorMessage;
            if (!requestHandler.TryParseTableId(Request, out tableId, out errorMessage))
                return Json(new { success = false, message = errorMessage });

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
            int tableId;
            string errorMessage;
            if (!requestHandler.TryParseTableId(Request, out tableId, out errorMessage))
                return Json(new { success = false, message = errorMessage });

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
            int tableId;
            string errorMessage;
            if (!requestHandler.TryParseTableId(Request, out tableId, out errorMessage))
                return Json(new { success = false, message = errorMessage });

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
                    return Json(new List<object>(), JsonRequestBehavior.AllowGet);

                var employees = employeeService.GetAvailableEmployees(activeShift.CashierId);
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
                    return Json(new { success = false, message = "Bàn chưa có đơn hàng" }, JsonRequestBehavior.AllowGet);
                
                return Json(new { success = true, items }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public JsonResult SplitTable(int sourceTableId, int targetTableId, string itemsToSplit)
        {
            try
            {
                var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                var items = serializer.Deserialize<List<TableOperationService.SplitItemModel>>(itemsToSplit);

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
            return Session["CashierId"] as int? ?? 1;
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