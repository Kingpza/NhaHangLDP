using NhaHangLDP.Filters;
using NhaHangLDP.Models;
using NhaHangLDP.Services;
using System;
using System.Drawing;
using System.Linq;
using System.Web.Mvc;

namespace NhaHangLDP.Controllers
{
    /// <summary>
    /// Controller quản lý tạo và hiển thị QR Code
    /// </summary>
    public class QRCodeController : Controller
    {
        private readonly NhaHangLDPEntities _db = new NhaHangLDPEntities();
        private readonly QRCodeService _qrService = new QRCodeService();

        #region Table QR Code

        /// <summary>
        /// Tạo QR Code cho bàn (trả về hình ảnh PNG)
        /// </summary>
        [CustomAuthorize("Admin", "Manager")]
        public ActionResult TableQRImage(int tableId, int size = 10)
        {
            var table = _db.RestaurantTable.Find(tableId);
            if (table == null)
            {
                return HttpNotFound("Không tìm thấy bàn");
            }

            var baseUrl = Request.Url.GetLeftPart(UriPartial.Authority);
            var qrResult = _qrService.GenerateTableQRCode(baseUrl, tableId, table.TableNumber);

            return File(qrResult.ImageBytes, "image/png", $"QR_Ban_{table.TableNumber}.png");
        }

        /// <summary>
        /// Trang hiển thị QR Code cho bàn (có thể in)
        /// </summary>
        [CustomAuthorize("Admin", "Manager")]
        public ActionResult TableQR(int tableId)
        {
            var table = _db.RestaurantTable
                .Include("TableArea")
                .FirstOrDefault(t => t.Id == tableId);

            if (table == null)
            {
                return HttpNotFound("Không tìm thấy bàn");
            }

            var baseUrl = Request.Url.GetLeftPart(UriPartial.Authority);
            var qrResult = _qrService.GenerateTableQRCode(baseUrl, tableId, table.TableNumber);

            ViewBag.Table = table;
            ViewBag.QRCode = qrResult;
            ViewBag.QRUrl = qrResult.Content;

            return View(qrResult);
        }

        /// <summary>
        /// Tạo QR Code cho tất cả bàn
        /// </summary>
        [CustomAuthorize("Admin", "Manager")]
        public ActionResult AllTablesQR()
        {
            var tables = _db.RestaurantTable
                .Include("TableArea")
                .Where(t => t.Status != "Deleted")
                .OrderBy(t => t.TableArea.Name)
                .ThenBy(t => t.TableNumber)
                .ToList();

            var baseUrl = Request.Url.GetLeftPart(UriPartial.Authority);
            var qrCodes = tables.Select(t => new TableQRViewModel
            {
                Table = t,
                QRCode = _qrService.GenerateTableQRCode(baseUrl, t.Id, t.TableNumber)
            }).ToList();

            return View(qrCodes);
        }

        #endregion

        #region Bill QR Code

        /// <summary>
        /// Tạo QR Code cho hóa đơn (trả về hình ảnh PNG)
        /// </summary>
        [CustomAuthorize("Admin", "Manager", "Cashier")]
        public ActionResult BillQRImage(int billId)
        {
            var bill = _db.Bill.Find(billId);
            if (bill == null)
            {
                return HttpNotFound("Không tìm thấy hóa đơn");
            }

            var qrResult = _qrService.GenerateBillQRCode(bill.Id, bill.FinalAmount, bill.BillDate);
            return File(qrResult.ImageBytes, "image/png", $"QR_HoaDon_{billId}.png");
        }

        /// <summary>
        /// Tạo QR Code thanh toán chuyển khoản
        /// </summary>
        [CustomAuthorize("Admin", "Manager", "Cashier")]
        public ActionResult BankTransferQR(int billId)
        {
            var bill = _db.Bill
                .Include("Order.RestaurantTable")
                .FirstOrDefault(b => b.Id == billId);

            if (bill == null)
            {
                return HttpNotFound("Không tìm thấy hóa đơn");
            }

            // Lấy thông tin ngân hàng từ cài đặt
            var bankSettings = GetBankSettings();
            var description = $"TT HD{bill.Id:D6} Ban{bill.Order?.RestaurantTable?.TableNumber ?? "NA"}";

            var qrResult = _qrService.GenerateBankTransferQRCode(
                bankSettings.BankId,
                bankSettings.AccountNumber,
                bankSettings.AccountName,
                bill.FinalAmount,
                description
            );

            ViewBag.Bill = bill;
            ViewBag.QRCode = qrResult;
            ViewBag.BankSettings = bankSettings;

            return View(qrResult);
        }

        /// <summary>
        /// API trả về QR Code thanh toán dạng JSON
        /// </summary>
        [HttpGet]
        public JsonResult GetBillPaymentQR(int billId)
        {
            try
            {
                var bill = _db.Bill.Find(billId);
                if (bill == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy hóa đơn" }, JsonRequestBehavior.AllowGet);
                }

                var qrResult = _qrService.GenerateBillQRCode(bill.Id, bill.FinalAmount, bill.BillDate);

                return Json(new
                {
                    success = true,
                    dataUrl = qrResult.DataUrl,
                    content = qrResult.Content,
                    description = qrResult.Description
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        #endregion

        #region Employee Check-in QR Code

        /// <summary>
        /// Tạo QR Code cho nhân viên check-in (trả về hình ảnh PNG)
        /// </summary>
        [CustomAuthorize("Admin", "HR")]
        public ActionResult EmployeeQRImage(int employeeId)
        {
            var employee = _db.Employee.Find(employeeId);
            if (employee == null)
            {
                return HttpNotFound("Không tìm thấy nhân viên");
            }

            var employeeCode = $"NV{employee.Id:D4}";
            var qrResult = _qrService.GenerateEmployeeCheckInQRCode(employee.Id, employeeCode, employee.FullName);

            return File(qrResult.ImageBytes, "image/png", $"QR_NhanVien_{employeeCode}.png");
        }

        /// <summary>
        /// Trang hiển thị QR Code check-in nhân viên
        /// </summary>
        [CustomAuthorize("Admin", "HR")]
        public ActionResult EmployeeQR(int employeeId)
        {
            var employee = _db.Employee
                .Include("Role")
                .FirstOrDefault(e => e.Id == employeeId);

            if (employee == null)
            {
                return HttpNotFound("Không tìm thấy nhân viên");
            }

            var employeeCode = $"NV{employee.Id:D4}";
            var qrResult = _qrService.GenerateEmployeeCheckInQRCode(employee.Id, employeeCode, employee.FullName);

            ViewBag.Employee = employee;
            ViewBag.EmployeeCode = employeeCode;
            ViewBag.QRCode = qrResult;

            return View(qrResult);
        }

        /// <summary>
        /// Tạo QR Code cho tất cả nhân viên
        /// </summary>
        [CustomAuthorize("Admin", "HR")]
        public ActionResult AllEmployeesQR()
        {
            var employees = _db.Employee
                .Include("Role")
                .Where(e => e.IsActive)
                .OrderBy(e => e.Role.RoleName)
                .ThenBy(e => e.FullName)
                .ToList();

            var qrCodes = employees.Select(e =>
            {
                var employeeCode = $"NV{e.Id:D4}";
                return new EmployeeQRViewModel
                {
                    Employee = e,
                    EmployeeCode = employeeCode,
                    QRCode = _qrService.GenerateEmployeeCheckInQRCode(e.Id, employeeCode, e.FullName)
                };
            }).ToList();

            return View(qrCodes);
        }

        /// <summary>
        /// Xử lý check-in nhân viên qua QR
        /// </summary>
        [HttpPost]
        [CustomAuthorize("Admin", "HR", "Manager")]
        public JsonResult ProcessEmployeeCheckIn(string qrContent)
        {
            try
            {
                // Parse QR content: EMP|EmployeeId|EmployeeCode|Date
                var parts = qrContent.Split('|');
                if (parts.Length < 3 || parts[0] != "EMP")
                {
                    return Json(new { success = false, message = "QR Code không hợp lệ" });
                }

                if (!int.TryParse(parts[1], out int employeeId))
                {
                    return Json(new { success = false, message = "Mã nhân viên không hợp lệ" });
                }

                var employee = _db.Employee.Find(employeeId);
                if (employee == null || !employee.IsActive)
                {
                    return Json(new { success = false, message = "Nhân viên không tồn tại hoặc đã nghỉ việc" });
                }

                // Kiểm tra đã check-in hôm nay chưa (dựa vào CheckInTime.Date)
                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);
                var existingAttendance = _db.Attendance
                    .FirstOrDefault(a => a.EmployeeId == employeeId && 
                                        a.CheckInTime >= today && 
                                        a.CheckInTime < tomorrow);

                if (existingAttendance != null)
                {
                    // Đã check-in, thực hiện check-out
                    if (existingAttendance.CheckOutTime.HasValue)
                    {
                        return Json(new
                        {
                            success = false,
                            message = $"Nhân viên {employee.FullName} đã check-out lúc {existingAttendance.CheckOutTime:HH:mm}"
                        });
                    }

                    existingAttendance.CheckOutTime = DateTime.Now;
                    // Tính số giờ làm việc
                    existingAttendance.WorkHours = (decimal)(DateTime.Now - existingAttendance.CheckInTime).TotalHours;
                    _db.SaveChanges();

                    return Json(new
                    {
                        success = true,
                        action = "checkout",
                        message = $"Check-out thành công: {employee.FullName}",
                        employeeName = employee.FullName,
                        time = DateTime.Now.ToString("HH:mm:ss")
                    });
                }
                else
                {
                    // Chưa check-in, thực hiện check-in
                    var attendance = new Attendance
                    {
                        EmployeeId = employeeId,
                        CheckInTime = DateTime.Now,
                        Status = "Present"
                    };
                    _db.Attendance.Add(attendance);
                    _db.SaveChanges();

                    return Json(new
                    {
                        success = true,
                        action = "checkin",
                        message = $"Check-in thành công: {employee.FullName}",
                        employeeName = employee.FullName,
                        time = DateTime.Now.ToString("HH:mm:ss")
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// Trang quét QR check-in nhân viên
        /// </summary>
        [CustomAuthorize("Admin", "HR", "Manager")]
        public ActionResult CheckInScanner()
        {
            return View();
        }

        #endregion

        #region Delivery Tracking QR Code

        /// <summary>
        /// Tạo QR Code cho đơn hàng giao hàng
        /// </summary>
        public ActionResult DeliveryQRImage(string orderCode)
        {
            if (string.IsNullOrEmpty(orderCode))
            {
                return HttpNotFound("Mã đơn hàng không hợp lệ");
            }

            var baseUrl = Request.Url.GetLeftPart(UriPartial.Authority);
            var qrResult = _qrService.GenerateDeliveryTrackingQRCode(baseUrl, orderCode);

            return File(qrResult.ImageBytes, "image/png", $"QR_DonHang_{orderCode}.png");
        }

        /// <summary>
        /// API trả về QR tracking dạng JSON
        /// </summary>
        [HttpGet]
        public JsonResult GetDeliveryTrackingQR(string orderCode)
        {
            try
            {
                var baseUrl = Request.Url.GetLeftPart(UriPartial.Authority);
                var qrResult = _qrService.GenerateDeliveryTrackingQRCode(baseUrl, orderCode);

                return Json(new
                {
                    success = true,
                    dataUrl = qrResult.DataUrl,
                    trackingUrl = qrResult.Content,
                    description = qrResult.Description
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        #endregion

        #region Voucher QR Code

        /// <summary>
        /// Tạo QR Code cho voucher
        /// </summary>
        [CustomAuthorize("Admin", "Manager")]
        public ActionResult VoucherQRImage(int voucherId)
        {
            var voucher = _db.Voucher.Find(voucherId);
            if (voucher == null)
            {
                return HttpNotFound("Không tìm thấy voucher");
            }

            var discountInfo = voucher.DiscountType == "Percentage" 
                ? $"Giảm {voucher.DiscountValue}%" 
                : $"Giảm {voucher.DiscountValue:N0}đ";

            var qrResult = _qrService.GenerateVoucherQRCode(voucher.Code, discountInfo);

            return File(qrResult.ImageBytes, "image/png", $"QR_Voucher_{voucher.Code}.png");
        }

        #endregion

        #region Reservation QR Code

        /// <summary>
        /// Tạo QR Code cho đặt bàn
        /// </summary>
        public ActionResult ReservationQRImage(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                return HttpNotFound("Mã đặt bàn không hợp lệ");
            }

            var baseUrl = Request.Url.GetLeftPart(UriPartial.Authority);
            var qrResult = _qrService.GenerateReservationQRCode(baseUrl, code);

            return File(qrResult.ImageBytes, "image/png", $"QR_DatBan_{code}.png");
        }

        #endregion

        #region Generic QR Code

        /// <summary>
        /// Tạo QR Code tùy chỉnh từ nội dung bất kỳ
        /// </summary>
        [HttpGet]
        public ActionResult Generate(string content, int size = 10)
        {
            if (string.IsNullOrEmpty(content))
            {
                return HttpNotFound("Nội dung không được để trống");
            }

            var qrBytes = _qrService.GenerateQRCode(content, size);
            return File(qrBytes, "image/png");
        }

        /// <summary>
        /// API tạo QR Code dạng Base64
        /// </summary>
        [HttpPost]
        public JsonResult GenerateBase64(string content, int size = 10)
        {
            try
            {
                if (string.IsNullOrEmpty(content))
                {
                    return Json(new { success = false, message = "Nội dung không được để trống" });
                }

                var dataUrl = _qrService.GenerateQRCodeDataUrl(content, size);

                return Json(new
                {
                    success = true,
                    dataUrl = dataUrl,
                    content = content
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region Helper Methods

        private BankSettings GetBankSettings()
        {
            var settings = _db.AppSetting.ToList();
            return new BankSettings
            {
                BankId = settings.FirstOrDefault(s => s.SettingKey == "BankId")?.SettingValue ?? "MB",
                AccountNumber = settings.FirstOrDefault(s => s.SettingKey == "BankAccountNumber")?.SettingValue ?? "0123456789",
                AccountName = settings.FirstOrDefault(s => s.SettingKey == "BankAccountName")?.SettingValue ?? "NHA HANG LDP"
            };
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _db.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    #region View Models

    public class TableQRViewModel
    {
        public RestaurantTable Table { get; set; }
        public QRCodeResult QRCode { get; set; }
    }

    public class EmployeeQRViewModel
    {
        public Employee Employee { get; set; }
        public string EmployeeCode { get; set; }
        public QRCodeResult QRCode { get; set; }
    }

    public class BankSettings
    {
        public string BankId { get; set; }
        public string AccountNumber { get; set; }
        public string AccountName { get; set; }
    }

    #endregion
}
