using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using NhaHangLDP.Data.Entities;
using System.Linq;
using NhaHangLDP.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace NhaHangLDP.Controllers
{
    public class ReturnManagementController : Controller
    {
        private NhaHangLDPEntities db = new NhaHangLDPEntities();

        private bool IsAuthorized()
        {
            var userRole = HttpContext.Session.GetString("UserRole");
            if (string.IsNullOrEmpty(userRole))
                return false;
            var role = userRole.ToLower();
            return role == "admin" || role == "manager";
        }

        // GET: ReturnManagement
        public ActionResult Index()
        {
            if (!IsAuthorized())
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
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
            if (!IsAuthorized())
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
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
                    var bill = localDb.Bill
                         .Include(b => b.Order)
                             .ThenInclude(o => o.OrderDetails)
                                 .ThenInclude(od => od.MenuItem)
                         .FirstOrDefault(b => b.Id == billId && b.Status == "Paid");

                    if (bill == null)
                    {
                        return Json(new BillSearchResultViewModel
                        {
                            Success = false,
                            Message = "Không tìm thấy hóa đơn hoặc hóa đơn chưa được thanh toán!"
                        });
                    }

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

                    var orderDetails = bill.Order.OrderDetail.Select(od => new OrderDetailViewModel
                    {
                        OrderDetailID = od.Id,
                        MenuItemID = od.MenuItemId,
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
                        Bill = new
                        {
                            bill.Id,
                            bill.BillDate,
                            bill.FinalAmount,
                            bill.PaymentMethod
                        },
                        OrderDetails = orderDetails
                    });
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
            if (!IsAuthorized())
            {
                return RedirectToAction("Login", "Account");
            }

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
                var originalBill = db.Bill
                    .FirstOrDefault(b => b.Id == model.BillID && b.Status == "Paid");

                if (originalBill == null)
                {
                    ModelState.AddModelError("BillID", "Hóa đơn không tồn tại hoặc chưa được thanh toán.");
                    return ReloadViewWithError(model);
                }

                var existingReturn = db.ReturnBill
                    .FirstOrDefault(r => r.OriginalBillID == model.BillID);

                if (existingReturn != null)
                {
                    ModelState.AddModelError("BillID", "Hóa đơn này đã được trả hàng trước đó.");
                    return ReloadViewWithError(model);
                }

                var itemsToReturn = model.ReturnItems?.Where(r => r.ReturnQuantity > 0).ToList();

                if (itemsToReturn == null || !itemsToReturn.Any())
                {
                    ModelState.AddModelError("", "Vui lòng chọn ít nhất một món để trả hàng.");
                    return ReloadViewWithError(model);
                }

                using (var transaction = db.Database.BeginTransaction())
                {
                    var returnBill = new ReturnBill
                    {
                        OriginalBillId = model.BillID,
                        EmployeeId = model.EmployeeID,
                        ReturnDate = DateTime.Now,
                        TotalRefundAmount = model.TotalRefundAmount,
                        Reason = model.Reason
                    };

                    db.ReturnBill.Add(returnBill);
                    db.SaveChanges();

                    foreach (var returnItem in itemsToReturn)
                    {
                        var returnDetail = new ReturnBillDetail
                        {
                            ReturnBillId = returnBill.ReturnBillId,
                            MenuItemId = returnItem.MenuItemID,
                            Quantity = returnItem.ReturnQuantity,
                            UnitPrice = returnItem.UnitPrice,
                            IsDamaged = returnItem.IsDamaged
                        };

                        db.ReturnBillDetail.Add(returnDetail);
                    }

                    db.SaveChanges();
                    UpdateInventoryAfterReturn(itemsToReturn);

                    transaction.Commit();
                    TempData["Success"] = $"Tạo phiếu trả hàng #{returnBill.ReturnBillID} thành công! Số tiền hoàn trả: {model.TotalRefundAmount:N0} ₫";
                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra khi tạo phiếu trả hàng: " + ex.Message;
                return ReloadViewWithError(model);
            }
        }

        // GET: ReturnManagement/Details/5
        public ActionResult Details(int id)
        {
            if (!IsAuthorized())
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                var returnBill = db.ReturnBill
                    .Include(r => r.OriginalBill)
                    .Include(r => r.Employee)
                    .Include(r => r.ReturnBillDetails)
                        .ThenInclude(rd => rd.MenuItem)
                    .FirstOrDefault(r => r.ReturnBillId == id);

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

        private void UpdateInventoryAfterReturn(List<ReturnItemViewModel> returnItems)
        {
            foreach (var returnItem in returnItems)
            {
                var menuItemIngredients = db.MenuItemIngredient
                    .Include(mi => mi.Ingredient)
                    .Where(mi => mi.MenuItemId == returnItem.MenuItemID)
                    .ToList();

                foreach (var ingredient in menuItemIngredients)
                {
                    var calculatedQuantity = ingredient.RequiredQuantity * returnItem.ReturnQuantity;

                    if (returnItem.IsDamaged)
                    {
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
                        ingredient.Ingredient.AvailableStock += calculatedQuantity;
                    }
                }
            }

            db.SaveChanges();
        }

        private int GetCurrentEmployeeId()
        {
            var employeeIdStr = HttpContext.Session.GetString("EmployeeId");
            if (!string.IsNullOrEmpty(employeeIdStr) && int.TryParse(employeeIdStr, out int employeeId))
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