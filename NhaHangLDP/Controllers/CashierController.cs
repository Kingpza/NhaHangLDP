using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using NhaHangLDP.Models;
using NhaHangLDP.Services;

namespace NhaHangLDP.Controllers
{
    public class CashierController : Controller
    {
        private NhaHangLDPEntities db = new NhaHangLDPEntities();
        private ShiftManagementService shiftService;
        private OrderManagementService orderService;
        private PaymentService paymentService;
        private TableManagementService tableService;
        private ReportService reportService;
        private DashboardService dashboardService;
        private OrderQueryService orderQueryService;
        private TableOperationService tableOperationService;

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
        }

        #region Shift Management

        public ActionResult OpenShift()
        {
            var activeShift = shiftService.GetActiveShift();
            if (activeShift != null)
            {
                return RedirectToAction("Dashboard");
            }
            return View();
        }

        [HttpPost]
        public ActionResult StartShift(decimal openingAmount, string notes)
        {
            var cashierId = GetCurrentCashierIdFromSession();
            string errorMessage;
            
            if (shiftService.StartShift(cashierId, openingAmount, notes, out errorMessage))
            {
                var activeShift = shiftService.GetActiveShift();
                Session["ActiveShiftId"] = activeShift.Id;
                Session["ShiftStartTime"] = activeShift.StartTime;

                return Json(new
                {
                    success = true,
                    message = "Mở ca thành công!",
                    shiftId = activeShift.Id,
                    openingAmount = openingAmount
                });
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
                    summary = new
                    {
                        TotalOrders = summary.TotalOrders,
                        TotalRevenue = summary.TotalRevenue,
                        CashInHand = summary.CashInHand
                    },
                    printUrl = Url.Action("PrintShiftRevenueReport", "Cashier", new { shiftId = summary.ShiftId })
                });
            }

            return Json(new { success = false, message = errorMessage });
        }

        public ActionResult PrintShiftRevenueReport(int? shiftId)
        {
            if (!shiftId.HasValue)
            {
                return RedirectToAction("Dashboard");
            }

            var viewModel = reportService.GenerateShiftReport(shiftId.Value);
            if (viewModel == null)
            {
                return RedirectToAction("Dashboard");
            }

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
            {
                return RedirectToAction("OpenShift");
            }

            Session["ActiveShiftId"] = activeShift.Id;
            Session["ShiftStartTime"] = activeShift.StartTime;
            Session["CashierName"] = activeShift.Employee?.FullName ?? "Thu Ngân";

            var viewModel = dashboardService.GetDashboardData(activeShift.Id);
            return View(viewModel);
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
                var viewModel = dashboardService.GetTableAreasData();
                return View(viewModel);
            }
            catch (Exception ex)
            {
                var emptyModel = new TableAreasViewModel { LastUpdated = DateTime.Now };
                TempData["Error"] = "Có lỗi xảy ra khi tải danh sách bàn: " + ex.Message;
                return View(emptyModel);
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
                var targetShiftId = shiftId ?? activeShift.Id;
                var viewModel = dashboardService.GetShiftDetails(targetShiftId);

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
                var invoiceViewModel = reportService.GenerateInvoice(numericOrderId);
                if (invoiceViewModel == null)
                {
                    return HttpNotFound("Không tìm thấy đơn hàng.");
                }

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
                {
                    return Json(new { success = false, message = "Vui lòng mở ca trước!" }, JsonRequestBehavior.AllowGet);
                }

                var tableStatus = orderQueryService.GetTableStatus(tableId);
                if (tableStatus == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy bàn!" }, JsonRequestBehavior.AllowGet);
                }

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
            {
                return Json(new { success = false, message = "Vui lòng mở ca trước!" });
            }

            string errorMessage;
            if (tableService.UpdateTableStatus(tableId, status, out errorMessage))
            {
                var table = db.RestaurantTable.Find(tableId);
                return Json(new
                {
                    success = true,
                    message = $"Đã cập nhật trạng thái bàn {table.TableNumber} thành công!",
                    data = new { tableId = table.Id, status = status }
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
            {
                return Json(new { success = false, message = "Vui lòng mở ca trước khi tạo đơn hàng!" });
            }

            var cashierId = GetCurrentCashierIdFromSession();
            int orderId;
            string errorMessage;

            if (orderService.CreateOrder(tableId, orderItems, customerNote, cashierId, activeShift.Id, out orderId, out errorMessage))
            {
                return Json(new
                {
                    success = true,
                    message = "Đơn hàng đã được tạo thành công!",
                    orderId = orderId
                });
            }

            return Json(new { success = false, message = errorMessage });
        }

        [HttpPost]
        public ActionResult UpdateOrderStatus(int orderId, string status)
        {
            if (shiftService.GetActiveShift() == null)
            {
                return Json(new { success = false, message = "Vui lòng mở ca trước!" });
            }

            string errorMessage;
            if (orderService.UpdateOrderStatus(orderId, status, out errorMessage))
            {
                return Json(new
                {
                    success = true,
                    message = $"Đã cập nhật trạng thái đơn hàng thành {GetStatusTextVietnamese(status)}!",
                    data = new { orderId = orderId, status = status }
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
            {
                return Json(new { success = false, message = "Vui lòng mở ca trước khi thanh toán!" });
            }

            var cashierId = GetCurrentCashierIdFromSession();
            int billId;
            decimal changeAmount;
            string errorMessage;

            if (paymentService.ProcessPayment(orderId, paymentMethod, receivedAmount, cashierId, activeShift.Id, out billId, out changeAmount, out errorMessage))
            {
                return Json(new
                {
                    success = true,
                    message = "Thanh toán thành công!",
                    billId = billId,
                    changeAmount = changeAmount
                });
            }

            return Json(new { success = false, message = errorMessage });
        }

        [HttpPost]
        public ActionResult GenerateInvoice(int orderId)
        {
            try
            {
                if (shiftService.GetActiveShift() == null)
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

        #region Orders Management API

        [HttpGet]
        public JsonResult GetOrdersData(string status = "all")
        {
            try
            {
                var activeShift = shiftService.GetActiveShift();
                if (activeShift == null)
                {
                    return Json(new { success = false, message = "Vui lòng mở ca trước khi xem đơn hàng!" }, JsonRequestBehavior.AllowGet);
                }

                var orders = orderQueryService.GetOrdersData(activeShift.Id, status);
                return Json(new { success = true, orders = orders }, JsonRequestBehavior.AllowGet);
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
                {
                    return Json(new { success = false, message = "Không tìm thấy đơn hàng!" }, JsonRequestBehavior.AllowGet);
                }

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
                {
                    return Json(new { success = false, message = "Vui lòng mở ca trước!" });
                }

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

                status = char.ToUpper(status[0]) + status.Substring(1).ToLower();

                string errorMessage;
                if (orderService.UpdateOrderStatus(orderId, status, out errorMessage))
                {
                    return Json(new
                    {
                        success = true,
                        message = $"Đã cập nhật trạng thái đơn hàng thành {GetStatusTextVietnamese(status)}!",
                        data = new { orderId = orderId, status = status }
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
                var orderIdStr = Request.Form["orderId"];

                if (string.IsNullOrEmpty(orderIdStr))
                {
                    return Json(new { success = false, message = "Thiếu thông tin đơn hàng!" });
                }

                if (!int.TryParse(orderIdStr, out int orderId))
                {
                    return Json(new { success = false, message = "Mã đơn hàng không hợp lệ!" });
                }

                string errorMessage;
                if (orderService.CancelOrder(orderId, out errorMessage))
                {
                    return Json(new
                    {
                        success = true,
                        message = $"Đơn hàng #{orderId} đã được hủy thành công!"
                    });
                }

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

            var cashierId = GetCurrentCashierIdFromSession();
            int billId;
            decimal changeAmount;
            string errorMessage;

            if (paymentService.ProcessPayment(orderId, paymentMethod, receivedAmount, cashierId, activeShift.Id, out billId, out changeAmount, out errorMessage))
            {
                return Json(new
                {
                    success = true,
                    message = "Thanh toán thành công!",
                    billId = billId,
                    changeAmount = changeAmount,
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
                {
                    return Json(new { success = false, message = "Ca làm việc không hoạt động." }, JsonRequestBehavior.AllowGet);
                }

                var orderId = orderQueryService.GetLastPaidOrderId(activeShift.Id);
                if (orderId.HasValue)
                {
                    return Json(new { success = true, orderId = orderId.Value.ToString() }, JsonRequestBehavior.AllowGet);
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
                var activeShift = shiftService.GetActiveShift();
                if (activeShift == null)
                {
                    return Json(new { success = false, message = "Ca làm việc không hoạt động." }, JsonRequestBehavior.AllowGet);
                }

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
                {
                    return Json(new { success = false, message = "Vui lòng mở ca trước!" }, JsonRequestBehavior.AllowGet);
                }

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
            var activeShift = shiftService.GetActiveShift();
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

            string errorMessage;
            bool success = false;
            string message = "";

            if (action == "assign")
            {
                success = tableService.AssignTable(tableId, customers, customerName, customerPhone, notes, out errorMessage);
                if (success)
                {
                    var table = db.RestaurantTable.Find(tableId);
                    message = $"Đã xếp {customers} khách vào bàn {table.TableNumber}";
                }
            }
            else if (action == "reserve")
            {
                success = tableService.ReserveTable(tableId, customers, customerName, customerPhone, notes, time, out errorMessage);
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
            {
                return Json(new { success = true, message = message });
            }

            return Json(new { success = false, message = errorMessage });
        }

        [HttpPost]
        public JsonResult ConfirmReservationAPI()
        {
            var tableIdStr = Request.Form["tableId"];
            if (!int.TryParse(tableIdStr, out int tableId))
            {
                return Json(new { success = false, message = "Mã bàn không hợp lệ!" });
            }

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
            var tableIdStr = Request.Form["tableId"];
            if (!int.TryParse(tableIdStr, out int tableId))
            {
                return Json(new { success = false, message = "Mã bàn không hợp lệ!" });
            }

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
            var tableIdStr = Request.Form["tableId"];
            if (!int.TryParse(tableIdStr, out int tableId))
            {
                return Json(new { success = false, message = "Mã bàn không hợp lệ!" });
            }

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

        #endregion

        #region Table Operations

        [HttpPost]
        public JsonResult TransferTable(int sourceTableId, int targetTableId)
        {
            if (shiftService.GetActiveShift() == null)
                return Json(new { success = false, message = "Vui lòng mở ca trước!" });

            string errorMessage;
            if (tableService.TransferTable(sourceTableId, targetTableId, out errorMessage))
            {
                return Json(new { success = true, message = "Chuyển bàn thành công!" });
            }

            return Json(new { success = false, message = errorMessage });
        }

        [HttpPost]
        public JsonResult MergeTables(int mainTableId, int secondaryTableId)
        {
            if (shiftService.GetActiveShift() == null)
                return Json(new { success = false, message = "Vui lòng mở ca trước!" });

            string errorMessage;
            if (tableService.MergeTables(mainTableId, secondaryTableId, out errorMessage))
            {
                return Json(new { success = true, message = "Gộp bàn thành công!" });
            }

            return Json(new { success = false, message = errorMessage });
        }

        [HttpGet]
        public JsonResult GetTableOrderItems(int tableId)
        {
            try
            {
                var items = orderQueryService.GetTableOrderItems(tableId);
                if (items == null)
                {
                    return Json(new { success = false, message = "Bàn chưa có đơn hàng" }, JsonRequestBehavior.AllowGet);
                }

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
            try
            {
                var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                var items = serializer.Deserialize<List<TableOperationService.SplitItemModel>>(itemsToSplit);

                string errorMessage;
                if (tableOperationService.SplitTable(sourceTableId, targetTableId, items, out errorMessage))
                {
                    return Json(new { success = true, message = "Tách bàn thành công!" });
                }

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