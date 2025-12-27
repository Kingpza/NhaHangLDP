namespace NhaHangLDP
{
    /// <summary>
    /// Filter configuration - deprecated in ASP.NET Core
    /// Global filters are configured in Program.cs or via attributes
    /// </summary>
    public class FilterConfig
    {
        // ASP.NET Core sử dụng middleware và filter khác
        // Filters được đăng ký trong Program.cs hoặc Startup.cs
        public static void RegisterGlobalFilters()
        {
            // No-op in ASP.NET Core - filters registered in Program.cs
        }
    }
}
