using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using NhaHangLDP.Models;
using NhaHangLDP.Filters;
using Microsoft.AspNetCore.Mvc;

namespace NhaHangLDP.Controllers
{
    /// <summary>
    /// Controller quản lý khuyến mãi
    /// </summary>
    public class PromotionController : Controller
    {
        private NhaHangLDPEntities db = new NhaHangLDPEntities();

        #region Management Pages

        /// <summary>
        /// Danh sách khuyến mãi (Admin/Manager)
        /// </summary>
        [CustomAuthorize("Admin", "Manager")]
        public ActionResult Index(string search = "", string status = "", string type = "")
        {
            try
            {
                var query = db.Promotion.AsQueryable();

                // Tìm kiếm
                if (!string.IsNullOrEmpty(search))
                {
                    search = search.ToLower();
                    query = query.Where(p => p.Code.ToLower().Contains(search) ||
                                            p.Name.ToLower().Contains(search));
                }

                // Lọc theo trạng thái
                if (!string.IsNullOrEmpty(status))
                {
                    var now = DateTime.Now;
                    switch (status.ToLower())
                    {
                        case "active":
                            query = query.Where(p => p.IsActive && p.StartDate <= now && p.EndDate >= now);
                            break;
                        case "inactive":
                            query = query.Where(p => !p.IsActive);
                            break;
                        case "expired":
                            query = query.Where(p => p.EndDate < now);
                            break;
                        case "upcoming":
                            query = query.Where(p => p.StartDate > now);
                            break;
                    }
                }

                // Lọc theo loại
                if (!string.IsNullOrEmpty(type))
                {
                    query = query.Where(p => p.DiscountType == type);
                }

                var promotions = query.OrderByDescending(p => p.CreatedDate).ToList();

                var viewModel = new PromotionListViewModel
                {
                    Promotions = promotions.Select(p => new PromotionItemViewModel
                    {
                        Id = p.Id,
                        Code = p.Code,
                        Name = p.Name,
                        Description = p.Description,
                        DiscountType = p.DiscountType,
                        DiscountValue = p.DiscountValue,
                        MinOrderValue = p.MinOrderValue,
                        MaxDiscountAmount = p.MaxDiscountAmount,
                        StartDate = p.StartDate,
                        EndDate = p.EndDate,
                        MaxUsageCount = p.MaxUsageCount,
                        UsedCount = p.UsedCount,
                        IsActive = p.IsActive,
                        ApplicableTo = p.ApplicableTo,
                        ApplicableDays = p.ApplicableDays,
                        HappyHourStart = p.HappyHourStart,
                        HappyHourEnd = p.HappyHourEnd
                    }).ToList(),
                    Stats = GetPromotionStats(),
                    SearchTerm = search,
                    StatusFilter = status,
                    TypeFilter = type
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra: " + ex.Message;
                return View(new PromotionListViewModel());
            }
        }

        /// <summary>
        /// Trang tạo khuyến mãi mới
        /// </summary>
        [CustomAuthorize("Admin", "Manager")]
        public ActionResult Create()
        {
            var model = new PromotionFormViewModel
            {
                IsEdit = false,
                StartDate = DateTime.Now,
                EndDate = DateTime.Now.AddMonths(1)
            };
            PrepareFormViewBag();
            return View(model);
        }

        /// <summary>
        /// Xử lý tạo khuyến mãi mới
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [CustomAuthorize("Admin", "Manager")]
        public ActionResult Create(PromotionFormViewModel model)
        {
            try
            {
                if (model.EndDate <= model.StartDate)
                {
                    ModelState.AddModelError("EndDate", "Ngày kết thúc phải sau ngày bắt đầu");
                }

                if (db.Promotion.Any(p => p.Code == model.Code))
                {
                    ModelState.AddModelError("Code", "Mã khuyến mãi đã tồn tại");
                }

                if (model.DiscountType == "Percentage" && model.DiscountValue > 100)
                {
                    ModelState.AddModelError("DiscountValue", "Phần trăm giảm không được vượt quá 100%");
                }

                if (ModelState.IsValid)
                {
                    var promotion = new Promotion
                    {
                        Code = model.Code.ToUpper().Trim(),
                        Name = model.Name,
                        Description = model.Description,
                        DiscountType = model.DiscountType,
                        DiscountValue = model.DiscountValue,
                        MinOrderValue = model.MinOrderValue,
                        MaxDiscountAmount = model.MaxDiscountAmount,
                        StartDate = model.StartDate,
                        EndDate = model.EndDate,
                        MaxUsageCount = model.MaxUsageCount,
                        MaxUsagePerCustomer = model.MaxUsagePerCustomer,
                        UsedCount = 0,
                        ApplicableTo = model.ApplicableTo,
                        ApplicableIds = model.ApplicableIds,
                        IsNewCustomerOnly = model.IsNewCustomerOnly,
                        HappyHourStart = model.HappyHourStart,
                        HappyHourEnd = model.HappyHourEnd,
                        ApplicableDays = model.ApplicableDays,
                        IsActive = model.IsActive,
                        CreatedDate = DateTime.Now,
                        CreatedBy = Session["Username"]?.ToString() ?? "Admin"
                    };

                    db.Promotion.Add(promotion);
                    db.SaveChanges();

                    TempData["Success"] = $"Đã tạo khuyến mãi '{promotion.Name}' thành công!";
                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra: " + ex.Message;
            }

            PrepareFormViewBag();
            return View(model);
        }

        /// <summary>
        /// Trang sửa khuyến mãi
        /// </summary>
        [CustomAuthorize("Admin", "Manager")]
        public ActionResult Edit(int id)
        {
            var promotion = db.Promotion.Find(id);
            if (promotion == null)
            {
                TempData["Error"] = "Không tìm thấy khuyến mãi";
                return RedirectToAction("Index");
            }

            var model = new PromotionFormViewModel
            {
                Id = promotion.Id,
                Code = promotion.Code,
                Name = promotion.Name,
                Description = promotion.Description,
                DiscountType = promotion.DiscountType,
                DiscountValue = promotion.DiscountValue,
                MinOrderValue = promotion.MinOrderValue,
                MaxDiscountAmount = promotion.MaxDiscountAmount,
                StartDate = promotion.StartDate,
                EndDate = promotion.EndDate,
                MaxUsageCount = promotion.MaxUsageCount,
                MaxUsagePerCustomer = promotion.MaxUsagePerCustomer,
                ApplicableTo = promotion.ApplicableTo,
                ApplicableIds = promotion.ApplicableIds,
                IsNewCustomerOnly = promotion.IsNewCustomerOnly,
                HappyHourStart = promotion.HappyHourStart,
                HappyHourEnd = promotion.HappyHourEnd,
                ApplicableDays = promotion.ApplicableDays,
                IsActive = promotion.IsActive,
                IsEdit = true
            };

            PrepareFormViewBag();
            return View("Create", model);
        }

        /// <summary>
        /// Xử lý cập nhật khuyến mãi
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [CustomAuthorize("Admin", "Manager")]
        public ActionResult Edit(PromotionFormViewModel model)
        {
            try
            {
                if (model.EndDate <= model.StartDate)
                {
                    ModelState.AddModelError("EndDate", "Ngày kết thúc phải sau ngày bắt đầu");
                }

                if (db.Promotion.Any(p => p.Code == model.Code && p.Id != model.Id))
                {
                    ModelState.AddModelError("Code", "Mã khuyến mãi đã tồn tại");
                }

                if (ModelState.IsValid)
                {
                    var promotion = db.Promotion.Find(model.Id);
                    if (promotion == null)
                    {
                        TempData["Error"] = "Không tìm thấy khuyến mãi";
                        return RedirectToAction("Index");
                    }

                    promotion.Code = model.Code.ToUpper().Trim();
                    promotion.Name = model.Name;
                    promotion.Description = model.Description;
                    promotion.DiscountType = model.DiscountType;
                    promotion.DiscountValue = model.DiscountValue;
                    promotion.MinOrderValue = model.MinOrderValue;
                    promotion.MaxDiscountAmount = model.MaxDiscountAmount;
                    promotion.StartDate = model.StartDate;
                    promotion.EndDate = model.EndDate;
                    promotion.MaxUsageCount = model.MaxUsageCount;
                    promotion.MaxUsagePerCustomer = model.MaxUsagePerCustomer;
                    promotion.ApplicableTo = model.ApplicableTo;
                    promotion.ApplicableIds = model.ApplicableIds;
                    promotion.IsNewCustomerOnly = model.IsNewCustomerOnly;
                    promotion.HappyHourStart = model.HappyHourStart;
                    promotion.HappyHourEnd = model.HappyHourEnd;
                    promotion.ApplicableDays = model.ApplicableDays;
                    promotion.IsActive = model.IsActive;
                    promotion.UpdatedDate = DateTime.Now;

                    db.SaveChanges();

                    TempData["Success"] = $"Đã cập nhật khuyến mãi '{promotion.Name}' thành công!";
                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra: " + ex.Message;
            }

            model.IsEdit = true;
            PrepareFormViewBag();
            return View("Create", model);
        }

        /// <summary>
        /// Toggle trạng thái khuyến mãi
        /// </summary>
        [HttpPost]
        [CustomAuthorize("Admin", "Manager")]
        public JsonResult ToggleStatus(int id)
        {
            try
            {
                var promotion = db.Promotion.Find(id);
                if (promotion == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy khuyến mãi" });
                }

                promotion.IsActive = !promotion.IsActive;
                promotion.UpdatedDate = DateTime.Now;
                db.SaveChanges();

                return Json(new
                {
                    success = true,
                    message = promotion.IsActive ? "Đã kích hoạt khuyến mãi" : "Đã tạm dừng khuyến mãi",
                    isActive = promotion.IsActive
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        /// <summary>
        /// Xóa khuyến mãi
        /// </summary>
        [HttpPost]
        [CustomAuthorize("Admin", "Manager")]
        public JsonResult Delete(int id)
        {
            try
            {
                var promotion = db.Promotion.Include(p => p.PromotionUsage).FirstOrDefault(p => p.Id == id);
                if (promotion == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy khuyến mãi" });
                }

                if (promotion.PromotionUsage.Any())
                {
                    return Json(new { success = false, message = "Không thể xóa khuyến mãi đã được sử dụng. Hãy tạm dừng thay vì xóa." });
                }

                db.Promotion.Remove(promotion);
                db.SaveChanges();

                return Json(new { success = true, message = "Đã xóa khuyến mãi thành công" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        /// <summary>
        /// Xem báo cáo sử dụng khuyến mãi
        /// </summary>
        [CustomAuthorize("Admin", "Manager")]
        public ActionResult UsageReport(int id)
        {
            var promotion = db.Promotion.Find(id);
            if (promotion == null)
            {
                TempData["Error"] = "Không tìm thấy khuyến mãi";
                return RedirectToAction("Index");
            }

            var usages = db.PromotionUsage
                .Include(u => u.Bill)
                .Where(u => u.PromotionId == id)
                .OrderByDescending(u => u.UsedDate)
                .ToList();

            var viewModel = new PromotionUsageReportViewModel
            {
                PromotionId = promotion.Id,
                PromotionCode = promotion.Code,
                PromotionName = promotion.Name,
                Usages = usages.Select(u => new PromotionUsageDetailViewModel
                {
                    Id = u.Id,
                    BillId = u.BillId,
                    CustomerPhone = u.CustomerPhone,
                    DiscountApplied = u.DiscountApplied,
                    UsedDate = u.UsedDate,
                    BillCode = u.BillId.HasValue ? $"HD{u.BillId.Value:D6}" : "N/A"
                }).ToList(),
                TotalDiscountGiven = usages.Sum(u => u.DiscountApplied),
                TotalUsageCount = usages.Count,
                FirstUsageDate = usages.Any() ? usages.Min(u => u.UsedDate) : (DateTime?)null,
                LastUsageDate = usages.Any() ? usages.Max(u => u.UsedDate) : (DateTime?)null
            };

            return View(viewModel);
        }

        #endregion

        #region API Endpoints

        /// <summary>
        /// API validate mã khuyến mãi
        /// </summary>
        [HttpPost]
        public JsonResult ValidatePromotion(string code, decimal orderTotal, string customerPhone = null)
        {
            try
            {
                if (string.IsNullOrEmpty(code))
                {
                    return Json(new PromotionValidationResult
                    {
                        IsValid = false,
                        Message = "Vui lòng nhập mã khuyến mãi"
                    });
                }

                code = code.ToUpper().Trim();
                var promotion = db.Promotion.FirstOrDefault(p => p.Code == code);

                if (promotion == null)
                {
                    return Json(new PromotionValidationResult
                    {
                        IsValid = false,
                        Message = "Mã khuyến mãi không tồn tại"
                    });
                }

                // Kiểm tra trạng thái
                if (!promotion.IsActive)
                {
                    return Json(new PromotionValidationResult
                    {
                        IsValid = false,
                        Message = "Mã khuyến mãi đã bị tạm dừng"
                    });
                }

                // Kiểm tra thời gian
                var now = DateTime.Now;
                if (now < promotion.StartDate)
                {
                    return Json(new PromotionValidationResult
                    {
                        IsValid = false,
                        Message = $"Mã khuyến mãi có hiệu lực từ {promotion.StartDate:dd/MM/yyyy}"
                    });
                }

                if (now > promotion.EndDate)
                {
                    return Json(new PromotionValidationResult
                    {
                        IsValid = false,
                        Message = "Mã khuyến mãi đã hết hạn"
                    });
                }

                // Kiểm tra số lượt sử dụng
                if (promotion.MaxUsageCount.HasValue && promotion.UsedCount >= promotion.MaxUsageCount.Value)
                {
                    return Json(new PromotionValidationResult
                    {
                        IsValid = false,
                        Message = "Mã khuyến mãi đã hết lượt sử dụng"
                    });
                }

                // Kiểm tra giá trị đơn hàng tối thiểu
                if (orderTotal < promotion.MinOrderValue)
                {
                    return Json(new PromotionValidationResult
                    {
                        IsValid = false,
                        Message = $"Đơn hàng tối thiểu {promotion.MinOrderValue:N0}đ để áp dụng mã này"
                    });
                }

                // Kiểm tra số lượt/khách hàng
                if (!string.IsNullOrEmpty(customerPhone) && promotion.MaxUsagePerCustomer.HasValue)
                {
                    var customerUsageCount = db.PromotionUsage
                        .Count(u => u.PromotionId == promotion.Id && u.CustomerPhone == customerPhone);

                    if (customerUsageCount >= promotion.MaxUsagePerCustomer.Value)
                    {
                        return Json(new PromotionValidationResult
                        {
                            IsValid = false,
                            Message = "Bạn đã sử dụng hết lượt cho mã khuyến mãi này"
                        });
                    }
                }

                // Kiểm tra Happy Hour
                if (promotion.HappyHourStart.HasValue && promotion.HappyHourEnd.HasValue)
                {
                    var currentTime = now.TimeOfDay;
                    if (currentTime < promotion.HappyHourStart.Value || currentTime > promotion.HappyHourEnd.Value)
                    {
                        return Json(new PromotionValidationResult
                        {
                            IsValid = false,
                            Message = $"Mã chỉ áp dụng trong khung giờ {promotion.HappyHourStart.Value:hh\\:mm} - {promotion.HappyHourEnd.Value:hh\\:mm}"
                        });
                    }
                }

                // Kiểm tra ngày trong tuần
                if (!string.IsNullOrEmpty(promotion.ApplicableDays))
                {
                    var todayName = now.DayOfWeek.ToString();
                    if (!promotion.ApplicableDays.Contains(todayName))
                    {
                        return Json(new PromotionValidationResult
                        {
                            IsValid = false,
                            Message = "Mã khuyến mãi không áp dụng cho hôm nay"
                        });
                    }
                }

                // Tính toán giảm giá
                decimal discount = 0;
                if (promotion.DiscountType == "Percentage")
                {
                    discount = orderTotal * promotion.DiscountValue / 100;
                    if (promotion.MaxDiscountAmount.HasValue && discount > promotion.MaxDiscountAmount.Value)
                    {
                        discount = promotion.MaxDiscountAmount.Value;
                    }
                }
                else // Fixed
                {
                    discount = promotion.DiscountValue;
                    if (discount > orderTotal)
                    {
                        discount = orderTotal;
                    }
                }

                return Json(new PromotionValidationResult
                {
                    IsValid = true,
                    Message = "Áp dụng mã khuyến mãi thành công!",
                    PromotionId = promotion.Id,
                    PromotionName = promotion.Name,
                    DiscountType = promotion.DiscountType,
                    DiscountValue = promotion.DiscountValue,
                    MaxDiscountAmount = promotion.MaxDiscountAmount,
                    CalculatedDiscount = discount
                });
            }
            catch (Exception ex)
            {
                return Json(new PromotionValidationResult
                {
                    IsValid = false,
                    Message = "Có lỗi xảy ra: " + ex.Message
                });
            }
        }

        /// <summary>
        /// API áp dụng khuyến mãi cho hóa đơn
        /// </summary>
        [HttpPost]
        public JsonResult ApplyPromotion(int billId, string code, string customerPhone = null)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var bill = db.Bill.Find(billId);
                    if (bill == null)
                    {
                        return Json(new { success = false, message = "Không tìm thấy hóa đơn" });
                    }

                    code = code.ToUpper().Trim();
                    var promotion = db.Promotion.FirstOrDefault(p => p.Code == code);
                    if (promotion == null)
                    {
                        return Json(new { success = false, message = "Mã khuyến mãi không tồn tại" });
                    }

                    // Tính toán giảm giá
                    decimal discount = 0;
                    if (promotion.DiscountType == "Percentage")
                    {
                        discount = bill.TotalAmount * promotion.DiscountValue / 100;
                        if (promotion.MaxDiscountAmount.HasValue && discount > promotion.MaxDiscountAmount.Value)
                        {
                            discount = promotion.MaxDiscountAmount.Value;
                        }
                    }
                    else
                    {
                        discount = promotion.DiscountValue;
                        if (discount > bill.TotalAmount)
                        {
                            discount = bill.TotalAmount;
                        }
                    }

                    // Cập nhật bill
                    bill.DiscountAmount = discount;
                    bill.FinalAmount = bill.TotalAmount - discount;

                    // Ghi nhận usage
                    var usage = new PromotionUsage
                    {
                        PromotionId = promotion.Id,
                        BillId = billId,
                        CustomerPhone = customerPhone,
                        DiscountApplied = discount,
                        UsedDate = DateTime.Now
                    };
                    db.PromotionUsage.Add(usage);

                    // Tăng số lượt đã sử dụng
                    promotion.UsedCount++;

                    db.SaveChanges();
                    transaction.Commit();

                    return Json(new
                    {
                        success = true,
                        message = "Áp dụng mã khuyến mãi thành công!",
                        discount = discount,
                        finalAmount = bill.FinalAmount
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
        /// API lấy danh sách khuyến mãi đang hoạt động
        /// </summary>
        [HttpGet]
        public JsonResult GetActivePromotions()
        {
            try
            {
                var now = DateTime.Now;
                var promotions = db.Promotion
                    .Where(p => p.IsActive && p.StartDate <= now && p.EndDate >= now)
                    .OrderBy(p => p.EndDate)
                    .Select(p => new
                    {
                        p.Id,
                        p.Code,
                        p.Name,
                        p.Description,
                        p.DiscountType,
                        p.DiscountValue,
                        p.MinOrderValue,
                        p.MaxDiscountAmount,
                        EndDate = p.EndDate,
                        DiscountDisplay = p.DiscountType == "Percentage" 
                            ? "Giảm " + p.DiscountValue + "%" 
                            : "Giảm " + p.DiscountValue + "đ"
                    })
                    .ToList();

                return Json(new { success = true, promotions = promotions }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        #endregion

        #region Helpers

        private PromotionStatsViewModel GetPromotionStats()
        {
            var now = DateTime.Now;
            return new PromotionStatsViewModel
            {
                TotalPromotions = db.Promotion.Count(),
                ActivePromotions = db.Promotion.Count(p => p.IsActive && p.StartDate <= now && p.EndDate >= now),
                ExpiredPromotions = db.Promotion.Count(p => p.EndDate < now),
                TotalUsageCount = db.PromotionUsage.Count(),
                TotalDiscountGiven = db.PromotionUsage.Sum(u => (decimal?)u.DiscountApplied) ?? 0
            };
        }

        private void PrepareFormViewBag()
        {
            ViewBag.DiscountTypes = new List<SelectListItem>
            {
                new SelectListItem { Value = "Percentage", Text = "Phần trăm (%)" },
                new SelectListItem { Value = "Fixed", Text = "Số tiền cố định (VNĐ)" }
            };

            ViewBag.ApplicableToOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = "All", Text = "Tất cả món" },
                new SelectListItem { Value = "Category", Text = "Theo danh mục" },
                new SelectListItem { Value = "Specific", Text = "Món cụ thể" }
            };

            ViewBag.DaysOfWeek = new List<SelectListItem>
            {
                new SelectListItem { Value = "Monday", Text = "Thứ Hai" },
                new SelectListItem { Value = "Tuesday", Text = "Thứ Ba" },
                new SelectListItem { Value = "Wednesday", Text = "Thứ Tư" },
                new SelectListItem { Value = "Thursday", Text = "Thứ Năm" },
                new SelectListItem { Value = "Friday", Text = "Thứ Sáu" },
                new SelectListItem { Value = "Saturday", Text = "Thứ Bảy" },
                new SelectListItem { Value = "Sunday", Text = "Chủ Nhật" }
            };

            ViewBag.Categories = db.MenuItem
                .Select(m => m.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToList();
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
