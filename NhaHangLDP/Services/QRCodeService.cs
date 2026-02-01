using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using QRCoder;

namespace NhaHangLDP.Services
{
    /// <summary>
    /// Service tạo QR Code sử dụng thư viện QRCoder
    /// </summary>
    public class QRCodeService
    {
        #region QR Code Generation

        /// <summary>
        /// Tạo QR Code dạng byte array (PNG)
        /// </summary>
        /// <param name="content">Nội dung cần mã hóa</param>
        /// <param name="pixelsPerModule">Kích thước mỗi module (pixel), mặc định 10</param>
        /// <returns>Byte array của hình ảnh PNG</returns>
        public byte[] GenerateQRCode(string content, int pixelsPerModule = 10)
        {
            if (string.IsNullOrEmpty(content))
                throw new ArgumentNullException(nameof(content));

            using (var qrGenerator = new QRCodeGenerator())
            {
                var qrCodeData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
                using (var qrCode = new QRCode(qrCodeData))
                {
                    using (var bitmap = qrCode.GetGraphic(pixelsPerModule))
                    {
                        using (var stream = new MemoryStream())
                        {
                            bitmap.Save(stream, ImageFormat.Png);
                            return stream.ToArray();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Tạo QR Code với logo ở giữa
        /// </summary>
        /// <param name="content">Nội dung cần mã hóa</param>
        /// <param name="logoPath">Đường dẫn tới file logo</param>
        /// <param name="pixelsPerModule">Kích thước mỗi module</param>
        /// <returns>Byte array của hình ảnh PNG</returns>
        public byte[] GenerateQRCodeWithLogo(string content, string logoPath, int pixelsPerModule = 10)
        {
            if (string.IsNullOrEmpty(content))
                throw new ArgumentNullException(nameof(content));

            using (var qrGenerator = new QRCodeGenerator())
            {
                var qrCodeData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.H);
                using (var qrCode = new QRCode(qrCodeData))
                {
                    Bitmap logo = null;
                    if (!string.IsNullOrEmpty(logoPath) && File.Exists(logoPath))
                    {
                        logo = new Bitmap(logoPath);
                    }

                    using (var bitmap = qrCode.GetGraphic(pixelsPerModule, Color.Black, Color.White, logo, 15, 6, true))
                    {
                        logo?.Dispose();
                        using (var stream = new MemoryStream())
                        {
                            bitmap.Save(stream, ImageFormat.Png);
                            return stream.ToArray();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Tạo QR Code với màu tùy chỉnh
        /// </summary>
        /// <param name="content">Nội dung cần mã hóa</param>
        /// <param name="darkColor">Màu tối (thường là đen)</param>
        /// <param name="lightColor">Màu sáng (thường là trắng)</param>
        /// <param name="pixelsPerModule">Kích thước mỗi module</param>
        /// <returns>Byte array của hình ảnh PNG</returns>
        public byte[] GenerateQRCodeWithColor(string content, Color darkColor, Color lightColor, int pixelsPerModule = 10)
        {
            if (string.IsNullOrEmpty(content))
                throw new ArgumentNullException(nameof(content));

            using (var qrGenerator = new QRCodeGenerator())
            {
                var qrCodeData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
                using (var qrCode = new QRCode(qrCodeData))
                {
                    using (var bitmap = qrCode.GetGraphic(pixelsPerModule, darkColor, lightColor, true))
                    {
                        using (var stream = new MemoryStream())
                        {
                            bitmap.Save(stream, ImageFormat.Png);
                            return stream.ToArray();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Chuyển đổi QR Code thành Base64 string để nhúng vào HTML
        /// </summary>
        /// <param name="content">Nội dung cần mã hóa</param>
        /// <param name="pixelsPerModule">Kích thước mỗi module</param>
        /// <returns>Base64 string</returns>
        public string GenerateQRCodeBase64(string content, int pixelsPerModule = 10)
        {
            var bytes = GenerateQRCode(content, pixelsPerModule);
            return Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// Tạo data URL cho QR Code (có thể dùng trực tiếp trong src của img tag)
        /// </summary>
        /// <param name="content">Nội dung cần mã hóa</param>
        /// <param name="pixelsPerModule">Kích thước mỗi module</param>
        /// <returns>Data URL string</returns>
        public string GenerateQRCodeDataUrl(string content, int pixelsPerModule = 10)
        {
            var base64 = GenerateQRCodeBase64(content, pixelsPerModule);
            return $"data:image/png;base64,{base64}";
        }

        #endregion

        #region QR Code For Specific Purposes

        /// <summary>
        /// Tạo QR Code cho bàn ăn (order)
        /// </summary>
        /// <param name="baseUrl">Base URL của website</param>
        /// <param name="tableId">ID bàn</param>
        /// <param name="tableNumber">Số bàn</param>
        /// <returns>QRCodeResult chứa thông tin QR</returns>
        public QRCodeResult GenerateTableQRCode(string baseUrl, int tableId, string tableNumber)
        {
            var orderUrl = $"{baseUrl.TrimEnd('/')}/QROrder/Scan?tableId={tableId}";
            var qrBytes = GenerateQRCode(orderUrl, 12);

            return new QRCodeResult
            {
                Content = orderUrl,
                ImageBytes = qrBytes,
                Base64Image = Convert.ToBase64String(qrBytes),
                DataUrl = $"data:image/png;base64,{Convert.ToBase64String(qrBytes)}",
                Description = $"QR Code cho Bàn {tableNumber}",
                Type = QRCodeType.TableOrder
            };
        }

        /// <summary>
        /// Tạo QR Code cho hóa đơn thanh toán
        /// </summary>
        /// <param name="billId">ID hóa đơn</param>
        /// <param name="amount">Số tiền</param>
        /// <param name="billDate">Ngày hóa đơn</param>
        /// <returns>QRCodeResult chứa thông tin QR</returns>
        public QRCodeResult GenerateBillQRCode(int billId, decimal amount, DateTime billDate)
        {
            // Format: HD|BillId|Amount|Date
            var content = $"HD{billId:D8}|{amount:F0}|{billDate:yyyyMMddHHmmss}";
            var qrBytes = GenerateQRCode(content, 10);

            return new QRCodeResult
            {
                Content = content,
                ImageBytes = qrBytes,
                Base64Image = Convert.ToBase64String(qrBytes),
                DataUrl = $"data:image/png;base64,{Convert.ToBase64String(qrBytes)}",
                Description = $"Hóa đơn #{billId} - {amount:N0} VNĐ",
                Type = QRCodeType.BillPayment
            };
        }

        /// <summary>
        /// Tạo QR Code cho hóa đơn thanh toán qua ngân hàng (VietQR format)
        /// </summary>
        /// <param name="bankId">Mã ngân hàng (VD: VCB, TCB, MB...)</param>
        /// <param name="accountNumber">Số tài khoản</param>
        /// <param name="accountName">Tên chủ tài khoản</param>
        /// <param name="amount">Số tiền</param>
        /// <param name="description">Nội dung chuyển khoản</param>
        /// <returns>QRCodeResult chứa thông tin QR</returns>
        public QRCodeResult GenerateBankTransferQRCode(string bankId, string accountNumber, string accountName, decimal amount, string description)
        {
            // VietQR format (simplified)
            var content = $"https://img.vietqr.io/image/{bankId}-{accountNumber}-compact.png?amount={amount:F0}&addInfo={Uri.EscapeDataString(description)}&accountName={Uri.EscapeDataString(accountName)}";
            var qrBytes = GenerateQRCode(content, 10);

            return new QRCodeResult
            {
                Content = content,
                ImageBytes = qrBytes,
                Base64Image = Convert.ToBase64String(qrBytes),
                DataUrl = $"data:image/png;base64,{Convert.ToBase64String(qrBytes)}",
                Description = $"Chuyển khoản {amount:N0} VNĐ - {description}",
                Type = QRCodeType.BankTransfer
            };
        }

        /// <summary>
        /// Tạo QR Code cho nhân viên check-in/check-out
        /// </summary>
        /// <param name="employeeId">ID nhân viên</param>
        /// <param name="employeeCode">Mã nhân viên</param>
        /// <param name="employeeName">Tên nhân viên</param>
        /// <returns>QRCodeResult chứa thông tin QR</returns>
        public QRCodeResult GenerateEmployeeCheckInQRCode(int employeeId, string employeeCode, string employeeName)
        {
            // Format: EMP|EmployeeId|EmployeeCode|Timestamp
            var content = $"EMP|{employeeId}|{employeeCode}|{DateTime.Now:yyyyMMdd}";
            var qrBytes = GenerateQRCode(content, 10);

            return new QRCodeResult
            {
                Content = content,
                ImageBytes = qrBytes,
                Base64Image = Convert.ToBase64String(qrBytes),
                DataUrl = $"data:image/png;base64,{Convert.ToBase64String(qrBytes)}",
                Description = $"Check-in: {employeeName} ({employeeCode})",
                Type = QRCodeType.EmployeeCheckIn
            };
        }

        /// <summary>
        /// Tạo QR Code cho đơn hàng giao hàng (delivery tracking)
        /// </summary>
        /// <param name="baseUrl">Base URL của website</param>
        /// <param name="orderCode">Mã đơn hàng</param>
        /// <returns>QRCodeResult chứa thông tin QR</returns>
        public QRCodeResult GenerateDeliveryTrackingQRCode(string baseUrl, string orderCode)
        {
            var trackingUrl = $"{baseUrl.TrimEnd('/')}/Checkout/TrackOrder?code={orderCode}";
            var qrBytes = GenerateQRCode(trackingUrl, 10);

            return new QRCodeResult
            {
                Content = trackingUrl,
                ImageBytes = qrBytes,
                Base64Image = Convert.ToBase64String(qrBytes),
                DataUrl = $"data:image/png;base64,{Convert.ToBase64String(qrBytes)}",
                Description = $"Theo dõi đơn hàng: {orderCode}",
                Type = QRCodeType.DeliveryTracking
            };
        }

        /// <summary>
        /// Tạo QR Code cho voucher/khuyến mãi
        /// </summary>
        /// <param name="voucherCode">Mã voucher</param>
        /// <param name="discountInfo">Thông tin giảm giá</param>
        /// <returns>QRCodeResult chứa thông tin QR</returns>
        public QRCodeResult GenerateVoucherQRCode(string voucherCode, string discountInfo)
        {
            var content = $"VOUCHER|{voucherCode}";
            var qrBytes = GenerateQRCode(content, 10);

            return new QRCodeResult
            {
                Content = content,
                ImageBytes = qrBytes,
                Base64Image = Convert.ToBase64String(qrBytes),
                DataUrl = $"data:image/png;base64,{Convert.ToBase64String(qrBytes)}",
                Description = $"Voucher: {voucherCode} - {discountInfo}",
                Type = QRCodeType.Voucher
            };
        }

        /// <summary>
        /// Tạo QR Code cho đặt bàn (reservation)
        /// </summary>
        /// <param name="baseUrl">Base URL của website</param>
        /// <param name="reservationCode">Mã đặt bàn</param>
        /// <returns>QRCodeResult chứa thông tin QR</returns>
        public QRCodeResult GenerateReservationQRCode(string baseUrl, string reservationCode)
        {
            var checkInUrl = $"{baseUrl.TrimEnd('/')}/Reservation/CheckIn?code={reservationCode}";
            var qrBytes = GenerateQRCode(checkInUrl, 10);

            return new QRCodeResult
            {
                Content = checkInUrl,
                ImageBytes = qrBytes,
                Base64Image = Convert.ToBase64String(qrBytes),
                DataUrl = $"data:image/png;base64,{Convert.ToBase64String(qrBytes)}",
                Description = $"Đặt bàn: {reservationCode}",
                Type = QRCodeType.Reservation
            };
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Lưu QR Code ra file
        /// </summary>
        /// <param name="content">Nội dung QR</param>
        /// <param name="filePath">Đường dẫn file</param>
        /// <param name="pixelsPerModule">Kích thước module</param>
        public void SaveQRCodeToFile(string content, string filePath, int pixelsPerModule = 10)
        {
            var bytes = GenerateQRCode(content, pixelsPerModule);
            File.WriteAllBytes(filePath, bytes);
        }

        /// <summary>
        /// Tạo nhiều QR Code cho danh sách bàn
        /// </summary>
        /// <param name="baseUrl">Base URL</param>
        /// <param name="tables">Danh sách bàn (Id, TableNumber)</param>
        /// <returns>Dictionary với key là TableId</returns>
        public System.Collections.Generic.Dictionary<int, QRCodeResult> GenerateMultipleTableQRCodes(
            string baseUrl, 
            System.Collections.Generic.IEnumerable<(int Id, string TableNumber)> tables)
        {
            var result = new System.Collections.Generic.Dictionary<int, QRCodeResult>();
            foreach (var table in tables)
            {
                result[table.Id] = GenerateTableQRCode(baseUrl, table.Id, table.TableNumber);
            }
            return result;
        }

        #endregion
    }

    #region Result Classes

    /// <summary>
    /// Kết quả tạo QR Code
    /// </summary>
    public class QRCodeResult
    {
        /// <summary>
        /// Nội dung được mã hóa trong QR
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// Dữ liệu hình ảnh dạng byte array
        /// </summary>
        public byte[] ImageBytes { get; set; }

        /// <summary>
        /// Dữ liệu hình ảnh dạng Base64
        /// </summary>
        public string Base64Image { get; set; }

        /// <summary>
        /// Data URL có thể dùng trực tiếp trong img src
        /// </summary>
        public string DataUrl { get; set; }

        /// <summary>
        /// Mô tả QR Code
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Loại QR Code
        /// </summary>
        public QRCodeType Type { get; set; }
    }

    /// <summary>
    /// Loại QR Code
    /// </summary>
    public enum QRCodeType
    {
        /// <summary>
        /// QR Code cho bàn order
        /// </summary>
        TableOrder,

        /// <summary>
        /// QR Code hóa đơn thanh toán
        /// </summary>
        BillPayment,

        /// <summary>
        /// QR Code chuyển khoản ngân hàng
        /// </summary>
        BankTransfer,

        /// <summary>
        /// QR Code check-in nhân viên
        /// </summary>
        EmployeeCheckIn,

        /// <summary>
        /// QR Code theo dõi giao hàng
        /// </summary>
        DeliveryTracking,

        /// <summary>
        /// QR Code voucher/khuyến mãi
        /// </summary>
        Voucher,

        /// <summary>
        /// QR Code đặt bàn
        /// </summary>
        Reservation,

        /// <summary>
        /// QR Code tùy chỉnh
        /// </summary>
        Custom
    }

    #endregion
}
