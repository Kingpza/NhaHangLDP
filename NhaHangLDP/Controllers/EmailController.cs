using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using NhaHangLDP.Models;
using NhaHangLDP.Services;
using NhaHangLDP.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace NhaHangLDP.Controllers
{
    /// <summary>
    /// Controller quản lý Email Settings và gửi email
    /// </summary>
    public class EmailController : Controller
    {
        private NhaHangLDPEntities db = new NhaHangLDPEntities();
        private EmailService _emailService;

        public EmailController()
        {
            _emailService = new EmailService();
        }

        #region Settings Pages

        /// <summary>
        /// Trang cài đặt email tổng quan
        /// </summary>
        [CustomAuthorize("Admin", "Manager")]
        public ActionResult Settings()
        {
            var config = db.EmailConfig.FirstOrDefault(c => c.IsActive) ?? new EmailConfig();
            var templates = db.EmailTemplate.OrderBy(t => t.Name).ToList();
            var recentLogs = db.EmailLog.OrderByDescending(l => l.CreatedDate).Take(10).ToList();

            var viewModel = new EmailSettingsViewModel
            {
                Config = new EmailConfigViewModel
                {
                    Id = config.Id,
                    SmtpServer = config.SmtpServer,
                    SmtpPort = config.SmtpPort,
                    SmtpUsername = config.SmtpUsername,
                    EnableSsl = config.EnableSsl,
                    FromEmail = config.FromEmail,
                    FromName = config.FromName,
                    DefaultCc = config.DefaultCc,
                    IsActive = config.IsActive
                },
                Templates = templates.Select(t => new EmailTemplateViewModel
                {
                    Id = t.Id,
                    TemplateCode = t.TemplateCode,
                    Name = t.Name,
                    Subject = t.Subject,
                    IsActive = t.IsActive
                }).ToList(),
                RecentLogs = new EmailLogListViewModel
                {
                    Logs = recentLogs.Select(l => new EmailLogViewModel
                    {
                        Id = l.Id,
                        EmailType = l.EmailType,
                        ToEmail = l.ToEmail,
                        Subject = l.Subject,
                        Status = l.Status,
                        CreatedDate = l.CreatedDate,
                        SentDate = l.SentDate
                    }).ToList()
                },
                Stats = _emailService.GetEmailStats()
            };

            return View(viewModel);
        }

        /// <summary>
        /// Lưu cấu hình SMTP
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [CustomAuthorize("Admin")]
        public ActionResult SaveConfig(EmailConfigViewModel model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    EmailConfig config;
                    if (model.Id > 0)
                    {
                        config = db.EmailConfig.Find(model.Id);
                        if (config == null)
                        {
                            TempData["Error"] = "Không tìm thấy cấu hình";
                            return RedirectToAction("Settings");
                        }
                    }
                    else
                    {
                        config = new EmailConfig { CreatedDate = DateTime.Now };
                        db.EmailConfig.Add(config);
                    }

                    config.SmtpServer = model.SmtpServer;
                    config.SmtpPort = model.SmtpPort;
                    config.SmtpUsername = model.SmtpUsername;
                    if (!string.IsNullOrEmpty(model.SmtpPassword))
                    {
                        config.SmtpPassword = model.SmtpPassword;
                    }
                    config.EnableSsl = model.EnableSsl;
                    config.FromEmail = model.FromEmail;
                    config.FromName = model.FromName;
                    config.DefaultCc = model.DefaultCc;
                    config.IsActive = model.IsActive;
                    config.UpdatedDate = DateTime.Now;

                    // Deactivate other configs if this is active
                    if (model.IsActive)
                    {
                        foreach (var other in db.EmailConfig.Where(c => c.Id != config.Id))
                        {
                            other.IsActive = false;
                        }
                    }

                    db.SaveChanges();
                    TempData["Success"] = "Đã lưu cấu hình email thành công!";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra: " + ex.Message;
            }

            return RedirectToAction("Settings");
        }

        /// <summary>
        /// Gửi email test
        /// </summary>
        [HttpPost]
        [CustomAuthorize("Admin")]
        public async Task<JsonResult> SendTestEmail(string testEmail)
        {
            try
            {
                if (string.IsNullOrEmpty(testEmail))
                {
                    return Json(new { success = false, message = "Vui lòng nhập email" });
                }

                var result = await _emailService.SendTestEmailAsync(testEmail);
                return Json(new
                {
                    success = result.Success,
                    message = result.Message
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        #endregion

        #region Template Management

        /// <summary>
        /// Danh sách templates
        /// </summary>
        [CustomAuthorize("Admin", "Manager")]
        public ActionResult Templates()
        {
            var templates = db.EmailTemplate.OrderBy(t => t.Name).ToList();
            var viewModel = new EmailTemplateListViewModel
            {
                Templates = templates.Select(t => new EmailTemplateViewModel
                {
                    Id = t.Id,
                    TemplateCode = t.TemplateCode,
                    Name = t.Name,
                    Subject = t.Subject,
                    AvailablePlaceholders = t.AvailablePlaceholders,
                    IsActive = t.IsActive
                }).ToList(),
                TotalTemplates = templates.Count,
                ActiveTemplates = templates.Count(t => t.IsActive)
            };

            return View(viewModel);
        }

        /// <summary>
        /// Tạo template mới
        /// </summary>
        [CustomAuthorize("Admin", "Manager")]
        public ActionResult CreateTemplate()
        {
            var model = new EmailTemplateViewModel { IsEdit = false, IsActive = true };
            ViewBag.TemplateTypes = GetTemplateTypes();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [CustomAuthorize("Admin", "Manager")]
        public ActionResult CreateTemplate(EmailTemplateViewModel model)
        {
            try
            {
                if (db.EmailTemplate.Any(t => t.TemplateCode == model.TemplateCode))
                {
                    ModelState.AddModelError("TemplateCode", "Mã template đã tồn tại");
                }

                if (ModelState.IsValid)
                {
                    var template = new EmailTemplate
                    {
                        TemplateCode = model.TemplateCode,
                        Name = model.Name,
                        Subject = model.Subject,
                        HtmlBody = model.HtmlBody,
                        TextBody = model.TextBody,
                        AvailablePlaceholders = model.AvailablePlaceholders,
                        IsActive = model.IsActive,
                        CreatedDate = DateTime.Now
                    };

                    db.EmailTemplate.Add(template);
                    db.SaveChanges();

                    TempData["Success"] = "Đã tạo template thành công!";
                    return RedirectToAction("Templates");
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra: " + ex.Message;
            }

            ViewBag.TemplateTypes = GetTemplateTypes();
            return View(model);
        }

        /// <summary>
        /// Sửa template
        /// </summary>
        [CustomAuthorize("Admin", "Manager")]
        public ActionResult EditTemplate(int id)
        {
            var template = db.EmailTemplate.Find(id);
            if (template == null)
            {
                TempData["Error"] = "Không tìm thấy template";
                return RedirectToAction("Templates");
            }

            var model = new EmailTemplateViewModel
            {
                Id = template.Id,
                TemplateCode = template.TemplateCode,
                Name = template.Name,
                Subject = template.Subject,
                HtmlBody = template.HtmlBody,
                TextBody = template.TextBody,
                AvailablePlaceholders = template.AvailablePlaceholders,
                IsActive = template.IsActive,
                IsEdit = true
            };

            ViewBag.TemplateTypes = GetTemplateTypes();
            return View("CreateTemplate", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [CustomAuthorize("Admin", "Manager")]
        public ActionResult EditTemplate(EmailTemplateViewModel model)
        {
            try
            {
                if (db.EmailTemplate.Any(t => t.TemplateCode == model.TemplateCode && t.Id != model.Id))
                {
                    ModelState.AddModelError("TemplateCode", "Mã template đã tồn tại");
                }

                if (ModelState.IsValid)
                {
                    var template = db.EmailTemplate.Find(model.Id);
                    if (template == null)
                    {
                        TempData["Error"] = "Không tìm thấy template";
                        return RedirectToAction("Templates");
                    }

                    template.TemplateCode = model.TemplateCode;
                    template.Name = model.Name;
                    template.Subject = model.Subject;
                    template.HtmlBody = model.HtmlBody;
                    template.TextBody = model.TextBody;
                    template.AvailablePlaceholders = model.AvailablePlaceholders;
                    template.IsActive = model.IsActive;
                    template.UpdatedDate = DateTime.Now;

                    db.SaveChanges();

                    TempData["Success"] = "Đã cập nhật template thành công!";
                    return RedirectToAction("Templates");
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Có lỗi xảy ra: " + ex.Message;
            }

            model.IsEdit = true;
            ViewBag.TemplateTypes = GetTemplateTypes();
            return View("CreateTemplate", model);
        }

        /// <summary>
        /// Xóa template
        /// </summary>
        [HttpPost]
        [CustomAuthorize("Admin")]
        public JsonResult DeleteTemplate(int id)
        {
            try
            {
                var template = db.EmailTemplate.Find(id);
                if (template == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy template" });
                }

                db.EmailTemplate.Remove(template);
                db.SaveChanges();

                return Json(new { success = true, message = "Đã xóa template" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        /// <summary>
        /// Preview template
        /// </summary>
        [HttpPost]
        [CustomAuthorize("Admin", "Manager")]
        public JsonResult PreviewTemplate(string htmlBody)
        {
            try
            {
                // Thay thế các placeholder bằng dữ liệu mẫu
                var previewHtml = htmlBody
                    .Replace("{{CustomerName}}", "Nguyễn Văn A")
                    .Replace("{{BookingDate}}", DateTime.Now.AddDays(1).ToString("dd/MM/yyyy"))
                    .Replace("{{BookingTime}}", "19:00")
                    .Replace("{{GuestCount}}", "4")
                    .Replace("{{BookingCode}}", "BK000001")
                    .Replace("{{RestaurantName}}", "Nhà Hàng LDP")
                    .Replace("{{RestaurantPhone}}", "0123 456 789")
                    .Replace("{{RestaurantAddress}}", "123 Đường ABC, Quận 1, TP.HCM")
                    .Replace("{{CurrentYear}}", DateTime.Now.Year.ToString())
                    .Replace("{{CurrentDate}}", DateTime.Now.ToString("dd/MM/yyyy"));

                return Json(new { success = true, html = previewHtml });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        #endregion

        #region Email Logs

        /// <summary>
        /// Xem log gửi email
        /// </summary>
        [CustomAuthorize("Admin", "Manager")]
        public ActionResult Logs(string status = "", string type = "", int page = 1)
        {
            var query = db.EmailLog.AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(l => l.Status == status);
            }

            if (!string.IsNullOrEmpty(type))
            {
                query = query.Where(l => l.EmailType == type);
            }

            var pageSize = 20;
            var totalLogs = query.Count();
            var totalPages = (int)Math.Ceiling(totalLogs / (double)pageSize);

            var logs = query
                .OrderByDescending(l => l.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var viewModel = new EmailLogListViewModel
            {
                Logs = logs.Select(l => new EmailLogViewModel
                {
                    Id = l.Id,
                    EmailType = l.EmailType,
                    ToEmail = l.ToEmail,
                    ToName = l.ToName,
                    Subject = l.Subject,
                    Status = l.Status,
                    ErrorMessage = l.ErrorMessage,
                    RetryCount = l.RetryCount,
                    CreatedDate = l.CreatedDate,
                    SentDate = l.SentDate,
                    ReferenceId = l.ReferenceId,
                    ReferenceType = l.ReferenceType
                }).ToList(),
                TotalLogs = totalLogs,
                SentCount = db.EmailLog.Count(l => l.Status == "Sent"),
                FailedCount = db.EmailLog.Count(l => l.Status == "Failed"),
                PendingCount = db.EmailLog.Count(l => l.Status == "Pending"),
                StatusFilter = status,
                TypeFilter = type,
                CurrentPage = page,
                PageSize = pageSize,
                TotalPages = totalPages
            };

            ViewBag.EmailTypes = GetEmailTypes();
            return View(viewModel);
        }

        /// <summary>
        /// Xem chi tiết log
        /// </summary>
        [CustomAuthorize("Admin", "Manager")]
        public ActionResult LogDetail(int id)
        {
            var log = db.EmailLog.Find(id);
            if (log == null)
            {
                TempData["Error"] = "Không tìm thấy log";
                return RedirectToAction("Logs");
            }

            var viewModel = new EmailLogViewModel
            {
                Id = log.Id,
                EmailType = log.EmailType,
                ToEmail = log.ToEmail,
                ToName = log.ToName,
                Subject = log.Subject,
                Body = log.Body,
                Status = log.Status,
                ErrorMessage = log.ErrorMessage,
                RetryCount = log.RetryCount,
                CreatedDate = log.CreatedDate,
                SentDate = log.SentDate,
                ReferenceId = log.ReferenceId,
                ReferenceType = log.ReferenceType
            };

            return View(viewModel);
        }

        /// <summary>
        /// Gửi lại email failed
        /// </summary>
        [HttpPost]
        [CustomAuthorize("Admin")]
        public async Task<JsonResult> RetryEmail(int logId)
        {
            try
            {
                var log = db.EmailLog.Find(logId);
                if (log == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy log" });
                }

                var result = await _emailService.SendEmailAsync(new SendEmailDto
                {
                    ToEmail = log.ToEmail,
                    ToName = log.ToName,
                    Subject = log.Subject,
                    Body = log.Body,
                    IsHtml = true,
                    EmailType = log.EmailType,
                    ReferenceId = log.ReferenceId,
                    ReferenceType = log.ReferenceType
                });

                // Cập nhật log cũ
                if (result.Success)
                {
                    log.Status = "Resent";
                    log.ErrorMessage = $"Đã gửi lại thành công vào {DateTime.Now:HH:mm dd/MM/yyyy}";
                    db.SaveChanges();
                }

                return Json(new { success = result.Success, message = result.Message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        /// <summary>
        /// Retry tất cả email failed
        /// </summary>
        [HttpPost]
        [CustomAuthorize("Admin")]
        public async Task<JsonResult> RetryAllFailed()
        {
            try
            {
                var count = await _emailService.RetryFailedEmailsAsync();
                return Json(new { success = true, message = $"Đã gửi lại {count} email" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        #endregion

        #region Send Email API

        /// <summary>
        /// API gửi email từ template
        /// </summary>
        [HttpPost]
        [CustomAuthorize("Admin", "Manager", "Cashier")]
        public async Task<JsonResult> SendTemplateEmail(SendTemplateEmailDto dto)
        {
            try
            {
                var result = await _emailService.SendTemplateEmailAsync(dto);
                return Json(new { success = result.Success, message = result.Message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        /// <summary>
        /// Gửi email xác nhận đặt bàn
        /// </summary>
        [HttpPost]
        [CustomAuthorize("Admin", "Manager", "Cashier")]
        public async Task<JsonResult> SendBookingConfirmation(int bookingId, string email)
        {
            try
            {
                var booking = db.Booking.Find(bookingId);
                if (booking == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy đặt bàn" });
                }

                var result = await _emailService.SendBookingConfirmationAsync(booking, email);
                return Json(new { success = result.Success, message = result.Message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        /// <summary>
        /// Gửi hóa đơn qua email
        /// </summary>
        [HttpPost]
        [CustomAuthorize("Admin", "Manager", "Cashier")]
        public async Task<JsonResult> SendInvoice(int billId, string email, string customerName)
        {
            try
            {
                var bill = db.Bill.Find(billId);
                if (bill == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy hóa đơn" });
                }

                var result = await _emailService.SendInvoiceEmailAsync(bill, email, customerName);
                return Json(new { success = result.Success, message = result.Message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        #endregion

        #region Helpers

        private List<SelectListItem> GetTemplateTypes()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Value = EmailTypes.BookingConfirmation, Text = "Xác nhận đặt bàn" },
                new SelectListItem { Value = EmailTypes.BookingReminder, Text = "Nhắc nhở đặt bàn" },
                new SelectListItem { Value = EmailTypes.BookingCancellation, Text = "Hủy đặt bàn" },
                new SelectListItem { Value = EmailTypes.OrderConfirmation, Text = "Xác nhận đơn hàng" },
                new SelectListItem { Value = EmailTypes.OrderReady, Text = "Đơn hàng sẵn sàng" },
                new SelectListItem { Value = EmailTypes.PromotionNotification, Text = "Thông báo khuyến mãi" },
                new SelectListItem { Value = EmailTypes.Welcome, Text = "Chào mừng" },
                new SelectListItem { Value = EmailTypes.Invoice, Text = "Hóa đơn" },
                new SelectListItem { Value = EmailTypes.Feedback, Text = "Feedback" },
                new SelectListItem { Value = EmailTypes.Newsletter, Text = "Newsletter" }
            };
        }

        private List<SelectListItem> GetEmailTypes()
        {
            var types = db.EmailLog.Select(l => l.EmailType).Distinct().ToList();
            return types.Select(t => new SelectListItem { Value = t, Text = t }).ToList();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
                _emailService?.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}
