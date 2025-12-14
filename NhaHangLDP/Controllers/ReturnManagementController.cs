using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using NhaHangLDP.Models;

namespace NhaHangLDP.Controllers
{
    public class ReturnManagementController : Controller
    {
        private NhaHangLDPEntities db = new NhaHangLDPEntities();

        // GET: ReturnManagement
        public ActionResult Index()
        {
            if (Session["UserRole"] == null ||
                (Session["UserRole"].ToString().ToLower() != "admin" &&
                Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                // Lấy danh sách tất cả các phiếu trả hàng
                var returnBills = db.ReturnBill
                    .Include(r => r.Bill)
                    .Include(r => r.Employee)
                    .Include(r => r.ReturnBillDetail)
                    .OrderByDescending(r => r.ReturnDate)
                    .ToList();

                return View(returnBills);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi tải danh sách phiếu trả hàng: " + ex.Message;
                return View(new List<ReturnBill>());
            }
        }

        // GET: ReturnManagement/Create
        public ActionResult Create()
        {
            if (Session["UserRole"] == null ||
               (Session["UserRole"].ToString().ToLower() != "admin" &&
                  Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                // Tạo danh sách nhân viên cho dropdown
                ViewBag.Employees = db.Employee
                             .Where(e => e.IsActive == true)
                             .Select(e => new SelectListItem
                             {
                                 Value = e.Id.ToString(),
                                 Text = e.FullName
                             })
                    .ToList();

                var viewModel = new ReturnManagementViewModel();
                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // POST: ReturnManagement/FindBill
        [HttpPost]
        public JsonResult FindBill(int billId)
        {
            using (var localDb = new NhaHangLDPEntities())
            {
                try
                {
                    // Tối ưu hóa Include: Chỉ tải những gì cần thiết
                    var bill = localDb.Bill
                         .Include(b => b.Order)
                         .Include(b => b.Order.OrderDetail.Select(od => od.MenuItem))
                         .FirstOrDefault(b => b.Id == billId && b.Status == "Paid");

                    if (bill == null)
                    {
                        return Json(new BillSearchResultViewModel
                        {
                            Success = false,
                            Message = "Không tìm thấy hóa đơn hoặc hóa đơn chưa được thanh toán!"
                        });
                    }

                    // Kiểm tra xem hóa đơn đã được trả hàng chưa
                    var existingReturn = localDb.ReturnBill
                                                .FirstOrDefault(r => r.OriginalBillID == billId);

                    if (existingReturn != null)
                    {
                        return Json(new BillSearchResultViewModel
                        {
                            Success = false,
                            Message = "Hóa đơn này đã được trả hàng trước đó!"
                        });
                    }

                    // Tạo danh sách chi tiết đơn hàng cho ViewModel
                    var orderDetails = bill.Order.OrderDetail.Select(od => new OrderDetailViewModel
                    {
                        OrderDetailID = od.Id,
                        MenuItemID = od.MenuItemId,
                        // Đảm bảo MenuItem được tải để có tên
                        MenuItemName = od.MenuItem?.Name,
                        Quantity = od.Quantity,
                        PriceAtTime = od.PriceAtTime,
                        TotalAmount = od.Quantity * od.PriceAtTime,
                        Notes = od.Notes
                    }).ToList();

                    return Json(new BillSearchResultViewModel
                    {
                        Success = true,
                        Message = "Tìm thấy hóa đơn thành công!",
                        Bill = new // Tạo đối tượng ẩn danh để tránh lỗi serialization vòng lặp
                        {
                            bill.Id,
                            bill.BillDate,
                            bill.FinalAmount,
                            bill.PaymentMethod // Giả định PaymentMethod là thuộc tính đơn giản có thể serialize
                        },
                        OrderDetails = orderDetails
                    }, JsonRequestBehavior.AllowGet);
                }
                catch (Exception ex)
                {
                    return Json(new BillSearchResultViewModel
                    {
                        Success = false,
                        Message = "Có lỗi xảy ra khi tìm kiếm hóa đơn: " + ex.Message
                    });
                }
            }
        }

        // POST: ReturnManagement/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(ReturnManagementViewModel model)
        {
            if (Session["UserRole"] == null ||
                   (Session["UserRole"].ToString().ToLower() != "admin" &&
               Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return RedirectToAction("Login", "Account");
            }

            // Tải lại ViewBag cho Dropdown nếu có lỗi xảy ra
            Func<ReturnManagementViewModel, ActionResult> ReloadViewWithError = (currentModel) =>
            {
                ViewBag.Employees = db.Employee
                   .Where(e => e.IsActive == true)
                   .Select(e => new SelectListItem
                   {
                       Value = e.Id.ToString(),
                       Text = e.FullName
                   })
                   .ToList();
                return View(currentModel);
            };
            if (!ModelState.IsValid)
            {
                return ReloadViewWithError(model);
            }

            try
            {
                // Kiểm tra hóa đơn có tồn tại và đã được thanh toán
                var originalBill = db.Bill
                    .FirstOrDefault(b => b.Id == model.BillID && b.Status == "Paid");

                if (originalBill == null)
                {
                    ModelState.AddModelError("BillID", "Hóa đơn không tồn tại hoặc chưa được thanh toán.");
                    return ReloadViewWithError(model);
                }

                // Kiểm tra xem hóa đơn đã được trả hàng chưa
                var existingReturn = db.ReturnBill
                    .FirstOrDefault(r => r.OriginalBillID == model.BillID);

                if (existingReturn != null)
                {
                    ModelState.AddModelError("BillID", "Hóa đơn này đã được trả hàng trước đó.");
                    return ReloadViewWithError(model);
                }

                // Lọc ra các món có số lượng trả > 0 để xử lý
                var itemsToReturn = model.ReturnItems?.Where(r => r.ReturnQuantity > 0).ToList();

                if (itemsToReturn == null || !itemsToReturn.Any())
                {
                    ModelState.AddModelError("", "Vui lòng chọn ít nhất một món để trả hàng.");
                    return ReloadViewWithError(model);
                }

                // --- BẮT ĐẦU TRANSACTION ---
                using (var transaction = db.Database.BeginTransaction())
                {
                    // Tạo phiếu trả hàng mới
                    var returnBill = new ReturnBill
                    {
                        OriginalBillID = model.BillID,
                        EmployeeID = model.EmployeeID,
                        ReturnDate = DateTime.Now,
                        TotalRefundAmount = model.TotalRefundAmount,
                        Reason = model.Reason
                    };

                    db.ReturnBill.Add(returnBill);
                    db.SaveChanges(); // Lưu để lấy ReturnBillID

                    // Tạo chi tiết phiếu trả hàng
                    foreach (var returnItem in itemsToReturn)
                    {
                        var returnDetail = new ReturnBillDetail
                        {
                            ReturnBillID = returnBill.ReturnBillID,
                            MenuItemID = returnItem.MenuItemID,
                            Quantity = returnItem.ReturnQuantity,
                            UnitPrice = returnItem.UnitPrice,
                            IsDamaged = returnItem.IsDamaged
                        };

                        db.ReturnBillDetail.Add(returnDetail);
                    }

                    db.SaveChanges();

                    // Cập nhật kho hàng (Logic quan trọng)
                    UpdateInventoryAfterReturn(itemsToReturn);

                    transaction.Commit(); // Hoàn tất giao dịch
                    TempData["Success"] = $"Tạo phiếu trả hàng #{returnBill.ReturnBillID} thành công! Số tiền hoàn trả: {model.TotalRefundAmount:N0} ₫";
                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                // Nếu có lỗi, rollback (nếu có transaction) và ghi lỗi
                TempData["Error"] = "Có lỗi xảy ra khi tạo phiếu trả hàng: " + ex.Message;
                return ReloadViewWithError(model);
            }
        }

        // GET: ReturnManagement/Details/5
        public ActionResult Details(int id)
        {
            if (Session["UserRole"] == null ||
            (Session["UserRole"].ToString().ToLower() != "admin" &&
                 Session["UserRole"].ToString().ToLower() != "manager"))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var returnBill = db.ReturnBill
                    .Include(r => r.Bill)
                    .Include(r => r.Employee)
                    .Include(r => r.ReturnBillDetail.Select(rd => rd.MenuItem))
                    .FirstOrDefault(r => r.ReturnBillID == id);

                if (returnBill == null)
                {
                    TempData["Error"] = "Không tìm thấy phiếu trả hàng.";
                    return RedirectToAction("Index");
                }

                return View(returnBill);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // Logic cập nhật kho hàng sau khi trả hàng
        private void UpdateInventoryAfterReturn(List<ReturnItemViewModel> returnItems)
        {
            foreach (var returnItem in returnItems)
            {
                // Tải nguyên liệu cần thiết cho món ăn
                var menuItemIngredients = db.MenuItemIngredient
                    .Include(mi => mi.Ingredient)
                    .Where(mi => mi.MenuItemId == returnItem.MenuItemID)
                    .ToList();

                foreach (var ingredient in menuItemIngredients)
                {
                    var calculatedQuantity = ingredient.RequiredQuantity * returnItem.ReturnQuantity;

                    if (returnItem.IsDamaged)
                    {
                        // Hàng bị hỏng, thêm vào bảng DamagedStock
                        var damagedStock = new DamagedStock
                        {
                            IngredientId = ingredient.IngredientId,
                            Quantity = calculatedQuantity,
                            DamageDate = DateTime.Now,
                            Reason = $"Trả hàng (HĐ #{returnItem.BillID}) - Món '{returnItem.MenuItemName}' bị hỏng",
                            ReportedByEmployeeId = GetCurrentEmployeeId()
                        };

                        db.DamagedStock.Add(damagedStock);
                    }
                    else
                    {
                        // Hàng còn tốt, cập nhật lại kho (tăng AvailableStock)
                        ingredient.Ingredient.AvailableStock += calculatedQuantity;
                    }
                }
            }

            // Lưu thay đổi vào database
            db.SaveChanges();
        }

        // Helper method để lấy ID nhân viên hiện tại
        private int GetCurrentEmployeeId()
        {
            if (Session["EmployeeId"] != null && int.TryParse(Session["EmployeeId"].ToString(), out int employeeId))
            {
                return employeeId;
            }
            var adminEmployee = db.Employee.FirstOrDefault(e => e.Role.RoleName == "Admin" && e.IsActive == true);
            return adminEmployee?.Id ?? 1;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}