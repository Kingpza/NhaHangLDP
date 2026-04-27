using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Data.Entity;
using NhaHangLDP.Models;

namespace NhaHangLDP.Services
{
    /// <summary>
    /// Service xử lý gửi email
    /// </summary>
    public class EmailService
    {
        private readonly NhaHangLDPEntities _db;

        public EmailService()
        {
            _db = new NhaHangLDPEntities();
        }

        public EmailService(NhaHangLDPEntities db)
        {
            _db = db;
        }

        /// <summary>
        /// Lấy cấu hình email đang active
        /// </summary>
        public EmailConfig GetActiveConfig()
        {
            return _db.EmailConfig.FirstOrDefault(c => c.IsActive);
        }

        /// <summary>
        /// Lấy template email theo mã
        /// </summary>
        public EmailTemplate GetTemplate(string templateCode)
        {
            return _db.EmailTemplate.FirstOrDefault(t => t.TemplateCode == templateCode && t.IsActive);
        }

        /// <summary>
        /// Gửi email đơn giản
        /// </summary>
        public async Task<EmailSendResult> SendEmailAsync(SendEmailDto dto)
        {
            var result = new EmailSendResult();
            var log = new EmailLog
            {
                EmailType = dto.EmailType ?? "CUSTOM",
                ToEmail = dto.ToEmail,
                ToName = dto.ToName,
                Subject = dto.Subject,
                Body = dto.Body,
                Status = "Pending",
                CreatedDate = DateTime.Now,
                RetryCount = 0,
                ReferenceId = dto.ReferenceId,
                ReferenceType = dto.ReferenceType
            };

            try
            {
                var config = GetActiveConfig();
                if (config == null)
                {
                    throw new Exception("Chưa cấu hình SMTP email. Vui lòng cấu hình trong phần Settings.");
                }

                using (var client = new SmtpClient(config.SmtpServer, config.SmtpPort))
                {
                    client.EnableSsl = config.EnableSsl;
                    client.Credentials = new NetworkCredential(config.SmtpUsername, config.SmtpPassword);
                    client.DeliveryMethod = SmtpDeliveryMethod.Network;
                    client.Timeout = 30000; // 30 seconds

                    var message = new MailMessage
                    {
                        From = new MailAddress(config.FromEmail, config.FromName),
                        Subject = dto.Subject,
                        Body = dto.Body,
                        IsBodyHtml = dto.IsHtml
                    };

                    message.To.Add(new MailAddress(dto.ToEmail, dto.ToName ?? dto.ToEmail));

                    // Thêm CC nếu có
                    if (!string.IsNullOrEmpty(dto.Cc))
                    {
                        foreach (var cc in dto.Cc.Split(','))
                        {
                            if (!string.IsNullOrWhiteSpace(cc))
                                message.CC.Add(cc.Trim());
                        }
                    }

                    // Thêm CC mặc định từ config
                    if (!string.IsNullOrEmpty(config.DefaultCc))
                    {
                        foreach (var cc in config.DefaultCc.Split(','))
                        {
                            if (!string.IsNullOrWhiteSpace(cc))
                                message.CC.Add(cc.Trim());
                        }
                    }

                    // Thêm BCC nếu có
                    if (!string.IsNullOrEmpty(dto.Bcc))
                    {
                        foreach (var bcc in dto.Bcc.Split(','))
                        {
                            if (!string.IsNullOrWhiteSpace(bcc))
                                message.Bcc.Add(bcc.Trim());
                        }
                    }

                    await client.SendMailAsync(message);

                    log.Status = "Sent";
                    log.SentDate = DateTime.Now;
                    result.Success = true;
                    result.Message = "Email đã được gửi thành công";
                    result.SentTime = DateTime.Now;
                }
            }
            catch (SmtpException smtpEx)
            {
                log.Status = "Failed";
                log.ErrorMessage = $"SMTP Error: {smtpEx.Message}";
                result.Success = false;
                result.Message = "Lỗi SMTP khi gửi email";
                result.ErrorDetails = smtpEx.ToString();
            }
            catch (Exception ex)
            {
                log.Status = "Failed";
                log.ErrorMessage = ex.Message;
                result.Success = false;
                result.Message = "Có lỗi xảy ra khi gửi email";
                result.ErrorDetails = ex.ToString();
            }

            // Lưu log
            _db.EmailLog.Add(log);
            await _db.SaveChangesAsync();
            result.EmailLogId = log.Id;

            return result;
        }

        /// <summary>
        /// Gửi email từ template
        /// </summary>
        public async Task<EmailSendResult> SendTemplateEmailAsync(SendTemplateEmailDto dto)
        {
            var template = GetTemplate(dto.TemplateCode);
            if (template == null)
            {
                return new EmailSendResult
                {
                    Success = false,
                    Message = $"Không tìm thấy template email với mã: {dto.TemplateCode}"
                };
            }

            var subject = ReplacePlaceholders(template.Subject, dto.Placeholders);
            var body = ReplacePlaceholders(template.HtmlBody, dto.Placeholders);

            return await SendEmailAsync(new SendEmailDto
            {
                ToEmail = dto.ToEmail,
                ToName = dto.ToName,
                Subject = subject,
                Body = body,
                IsHtml = true,
                EmailType = dto.TemplateCode,
                ReferenceId = dto.ReferenceId,
                ReferenceType = dto.ReferenceType
            });
        }

        /// <summary>
        /// Thay thế placeholders trong template
        /// </summary>
        private string ReplacePlaceholders(string template, Dictionary<string, string> placeholders)
        {
            if (string.IsNullOrEmpty(template) || placeholders == null)
                return template;

            foreach (var placeholder in placeholders)
            {
                template = template.Replace($"{{{{{placeholder.Key}}}}}", placeholder.Value);
                template = template.Replace($"[{placeholder.Key}]", placeholder.Value);
                template = template.Replace($"${{{placeholder.Key}}}", placeholder.Value);
            }

            // Thêm các placeholder mặc định
            template = template.Replace("{{CurrentYear}}", DateTime.Now.Year.ToString());
            template = template.Replace("{{CurrentDate}}", DateTime.Now.ToString("dd/MM/yyyy"));
            template = template.Replace("{{CurrentTime}}", DateTime.Now.ToString("HH:mm"));

            return template;
        }

        /// <summary>
        /// Gửi email xác nhận đặt bàn
        /// </summary>
        public async Task<EmailSendResult> SendBookingConfirmationAsync(Booking booking, string customerEmail)
        {
            var placeholders = new Dictionary<string, string>
            {
                { "CustomerName", booking.CustomerName },
                { "BookingDate", booking.BookingDateTime.ToString("dd/MM/yyyy") },
                { "BookingTime", booking.BookingDateTime.ToString("HH:mm") },
                { "GuestCount", booking.NumberOfGuests.ToString() },
                { "BookingCode", $"BK{booking.Id:D6}" },
                { "Notes", booking.Notes ?? "Không có" },
                { "RestaurantName", GetSetting("RestaurantName") ?? "Nhà hàng Hỷ Lạc Hotpot" },
                { "RestaurantPhone", GetSetting("PhoneNumber") ?? "" },
                { "RestaurantAddress", GetSetting("Address") ?? "" }
            };

            return await SendTemplateEmailAsync(new SendTemplateEmailDto
            {
                TemplateCode = EmailTypes.BookingConfirmation,
                ToEmail = customerEmail,
                ToName = booking.CustomerName,
                Placeholders = placeholders,
                ReferenceId = booking.Id,
                ReferenceType = "Booking"
            });
        }

        /// <summary>
        /// Gửi email nhắc nhở đặt bàn
        /// </summary>
        public async Task<EmailSendResult> SendBookingReminderAsync(Booking booking, string customerEmail)
        {
            var placeholders = new Dictionary<string, string>
            {
                { "CustomerName", booking.CustomerName },
                { "BookingDate", booking.BookingDateTime.ToString("dd/MM/yyyy") },
                { "BookingTime", booking.BookingDateTime.ToString("HH:mm") },
                { "GuestCount", booking.NumberOfGuests.ToString() },
                { "BookingCode", $"BK{booking.Id:D6}" },
                { "RestaurantName", GetSetting("RestaurantName") ?? "Nhà hàng Hỷ Lạc Hotpot" },
                { "RestaurantPhone", GetSetting("PhoneNumber") ?? "" },
                { "RestaurantAddress", GetSetting("Address") ?? "" }
            };

            return await SendTemplateEmailAsync(new SendTemplateEmailDto
            {
                TemplateCode = EmailTypes.BookingReminder,
                ToEmail = customerEmail,
                ToName = booking.CustomerName,
                Placeholders = placeholders,
                ReferenceId = booking.Id,
                ReferenceType = "Booking"
            });
        }

        /// <summary>
        /// Gửi email xác nhận đơn hàng QR
        /// </summary>
        public async Task<EmailSendResult> SendOrderConfirmationAsync(QROrder order, string customerEmail)
        {
            var orderItems = _db.QROrderDetail
                .Include(d => d.MenuItem)
                .Where(d => d.QROrderId == order.Id)
                .ToList();

            var itemsHtml = string.Join("", orderItems.Select(i => 
                $"<tr><td>{i.MenuItem.Name}</td><td>{i.Quantity}</td><td>{i.UnitPrice:N0}đ</td><td>{(i.Quantity * i.UnitPrice):N0}đ</td></tr>"));

            var totalAmount = orderItems.Sum(i => i.Quantity * i.UnitPrice);

            var placeholders = new Dictionary<string, string>
            {
                { "CustomerName", order.CustomerName ?? "Quý khách" },
                { "OrderCode", order.QROrderCode },
                { "TableNumber", order.RestaurantTable?.TableNumber ?? "" },
                { "OrderTime", order.CreatedTime.ToString("HH:mm dd/MM/yyyy") },
                { "OrderItems", itemsHtml },
                { "TotalAmount", totalAmount.ToString("N0") },
                { "Notes", order.Notes ?? "Không có" },
                { "RestaurantName", GetSetting("RestaurantName") ?? "Nhà hàng Hỷ Lạc Hotpot" },
                { "RestaurantPhone", GetSetting("PhoneNumber") ?? "" }
            };

            return await SendTemplateEmailAsync(new SendTemplateEmailDto
            {
                TemplateCode = EmailTypes.OrderConfirmation,
                ToEmail = customerEmail,
                ToName = order.CustomerName,
                Placeholders = placeholders,
                ReferenceId = order.Id,
                ReferenceType = "QROrder"
            });
        }

        /// <summary>
        /// Gửi email thông báo khuyến mãi
        /// </summary>
        public async Task<EmailSendResult> SendPromotionNotificationAsync(Promotion promotion, string customerEmail, string customerName)
        {
            var discountText = promotion.DiscountType == "Percentage" 
                ? $"Giảm {promotion.DiscountValue}%" 
                : $"Giảm {promotion.DiscountValue:N0}đ";

            var placeholders = new Dictionary<string, string>
            {
                { "CustomerName", customerName ?? "Quý khách" },
                { "PromotionName", promotion.Name },
                { "PromotionCode", promotion.Code },
                { "DiscountText", discountText },
                { "MinOrderValue", promotion.MinOrderValue.ToString("N0") },
                { "StartDate", promotion.StartDate.ToString("dd/MM/yyyy") },
                { "EndDate", promotion.EndDate.ToString("dd/MM/yyyy") },
                { "Description", promotion.Description ?? "" },
                { "RestaurantName", GetSetting("RestaurantName") ?? "Nhà hàng Hỷ Lạc Hotpot" }
            };

            return await SendTemplateEmailAsync(new SendTemplateEmailDto
            {
                TemplateCode = EmailTypes.PromotionNotification,
                ToEmail = customerEmail,
                ToName = customerName,
                Placeholders = placeholders,
                ReferenceId = promotion.Id,
                ReferenceType = "Promotion"
            });
        }

        /// <summary>
        /// Gửi email hóa đơn
        /// </summary>
        public async Task<EmailSendResult> SendInvoiceEmailAsync(Bill bill, string customerEmail, string customerName)
        {
            var order = _db.Order
                .Include(o => o.OrderDetail.Select(od => od.MenuItem))
                .Include(o => o.RestaurantTable)
                .FirstOrDefault(o => o.Id == bill.OrderId);

            if (order == null)
            {
                return new EmailSendResult
                {
                    Success = false,
                    Message = "Không tìm thấy đơn hàng"
                };
            }

            var itemsHtml = string.Join("", order.OrderDetail.Select(i =>
                $"<tr><td>{i.MenuItem.Name}</td><td>{i.Quantity}</td><td>{i.PriceAtTime:N0}đ</td><td>{(i.Quantity * i.PriceAtTime):N0}đ</td></tr>"));

            var placeholders = new Dictionary<string, string>
            {
                { "CustomerName", customerName ?? "Quý khách" },
                { "BillCode", $"HD{bill.Id:D6}" },
                { "BillDate", bill.BillDate.ToString("HH:mm dd/MM/yyyy") },
                { "TableNumber", order.RestaurantTable?.TableNumber ?? "" },
                { "OrderItems", itemsHtml },
                { "SubTotal", bill.TotalAmount.ToString("N0") },
                { "Discount", bill.DiscountAmount.ToString("N0") },
                { "TotalAmount", bill.FinalAmount.ToString("N0") },
                { "PaymentMethod", GetPaymentMethodText(bill.PaymentMethod) },
                { "RestaurantName", GetSetting("RestaurantName") ?? "Nhà hàng Hỷ Lạc Hotpot" },
                { "RestaurantAddress", GetSetting("Address") ?? "" },
                { "RestaurantPhone", GetSetting("PhoneNumber") ?? "" },
                { "RestaurantTaxCode", GetSetting("TaxCode") ?? "" }
            };

            return await SendTemplateEmailAsync(new SendTemplateEmailDto
            {
                TemplateCode = EmailTypes.Invoice,
                ToEmail = customerEmail,
                ToName = customerName,
                Placeholders = placeholders,
                ReferenceId = bill.Id,
                ReferenceType = "Bill"
            });
        }

        /// <summary>
        /// Gửi email test
        /// </summary>
        public async Task<EmailSendResult> SendTestEmailAsync(string toEmail)
        {
            return await SendEmailAsync(new SendEmailDto
            {
                ToEmail = toEmail,
                ToName = toEmail,
                Subject = "Email test từ hệ thống Nhà hàng Hỷ Lạc Hotpot",
                Body = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                        <h2 style='color: #dc7633;'>Email Test Thành Công!</h2>
                        <p>Xin chào,</p>
                        <p>Đây là email test từ hệ thống <strong>Nhà hàng Hỷ Lạc Hotpot</strong>.</p>
                        <p>Nếu bạn nhận được email này, cấu hình SMTP đã hoạt động đúng!</p>
                        <hr style='border: 1px solid #eee;'>
                        <p style='color: #666; font-size: 12px;'>
                            Gửi lúc: {DateTime.Now:HH:mm:ss dd/MM/yyyy}<br>
                            © {DateTime.Now.Year} Nhà hàng Hỷ Lạc Hotpot
                        </p>
                    </div>",
                IsHtml = true,
                EmailType = EmailTypes.Test
            });
        }

        /// <summary>
        /// Retry các email failed
        /// </summary>
        public async Task<int> RetryFailedEmailsAsync(int maxRetries = 3)
        {
            var failedEmails = _db.EmailLog
                .Where(e => e.Status == "Failed" && e.RetryCount < maxRetries)
                .OrderBy(e => e.CreatedDate)
                .Take(10)
                .ToList();

            int retryCount = 0;
            foreach (var log in failedEmails)
            {
                log.Status = "Retrying";
                log.RetryCount++;
                await _db.SaveChangesAsync();

                var result = await SendEmailAsync(new SendEmailDto
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

                if (result.Success)
                {
                    log.Status = "Sent";
                    log.SentDate = DateTime.Now;
                    log.ErrorMessage = null;
                    retryCount++;
                }
                else
                {
                    log.Status = "Failed";
                    log.ErrorMessage = result.ErrorDetails;
                }
                await _db.SaveChangesAsync();
            }

            return retryCount;
        }

        /// <summary>
        /// Lấy thống kê email
        /// </summary>
        public EmailStatsViewModel GetEmailStats()
        {
            var today = DateTime.Today;
            var weekStart = today.AddDays(-(int)today.DayOfWeek);
            var monthStart = new DateTime(today.Year, today.Month, 1);

            var stats = new EmailStatsViewModel
            {
                TotalSentToday = _db.EmailLog.Count(e => e.Status == "Sent" && DbFunctions.TruncateTime(e.SentDate) == today),
                TotalSentThisWeek = _db.EmailLog.Count(e => e.Status == "Sent" && e.SentDate >= weekStart),
                TotalSentThisMonth = _db.EmailLog.Count(e => e.Status == "Sent" && e.SentDate >= monthStart),
                FailedToday = _db.EmailLog.Count(e => e.Status == "Failed" && DbFunctions.TruncateTime(e.CreatedDate) == today)
            };

            var totalAttempts = _db.EmailLog.Count(e => DbFunctions.TruncateTime(e.CreatedDate) == today);
            stats.SuccessRate = totalAttempts > 0 ? (stats.TotalSentToday * 100.0 / totalAttempts) : 100;

            stats.EmailsByType = _db.EmailLog
                .Where(e => e.SentDate >= monthStart)
                .GroupBy(e => e.EmailType)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToDictionary(x => x.Type ?? "Other", x => x.Count);

            return stats;
        }

        private string GetSetting(string key)
        {
            return _db.AppSetting.FirstOrDefault(s => s.SettingKey == key)?.SettingValue;
        }

        private string GetPaymentMethodText(string method)
        {
            switch (method?.ToLower())
            {
                case "cash": return "Tiền mặt";
                case "card": return "Thẻ tín dụng";
                case "transfer": return "Chuyển khoản";
                case "ewallet": return "Ví điện tử";
                default: return method;
            }
        }

        public void Dispose()
        {
            _db?.Dispose();
        }
    }
}
