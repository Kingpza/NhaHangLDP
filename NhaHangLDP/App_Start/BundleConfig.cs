namespace NNhaHangLDP
{
    /// <summary>
    /// Bundle configuration - deprecated in ASP.NET Core
    /// Bundling is handled differently in ASP.NET Core using LibMan, WebOptimizer or during build process
    /// </summary>
    public class BundleConfig
    {
        // ASP.NET Core sử dụng bundling/minification khác với ASP.NET MVC
        // Các static files được serve trực tiếp từ wwwroot
        // Có thể sử dụng WebOptimizer hoặc bundler như webpack/gulp
        public static void RegisterBundles()
        {
            // No-op in ASP.NET Core
        }
    }
}
