namespace NhaHangLDP.Models
{
    /// <summary>
    /// ViewModel cho trang Error chuẩn .NET Core
    /// </summary>
    public class ErrorViewModel
    {
        public string RequestId { get; set; }
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
        public string Message { get; set; }
        public string StackTrace { get; set; }
    }
}
