using Microsoft.Owin;
using Owin;

[assembly: OwinStartup(typeof(NhaHangLDP.Startup))]

namespace NhaHangLDP
{
    /// <summary>
    /// OWIN Startup class cho cấu hình SignalR
    /// </summary>
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // Cấu hình SignalR
            app.MapSignalR();
        }
    }
}
