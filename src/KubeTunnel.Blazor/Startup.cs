using KubeTunnel.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MudBlazor.Services;
using System.Net.Http;

namespace KubeTunnel
{
    public static class Startup
    {
        public static IServiceProvider? Services { get; private set; }

        public static void Init()
        {
            var host = Host.CreateDefaultBuilder()
                           .ConfigureServices(WireUpServices)
                           .Build();
            Services = host.Services;
        }

        private static void WireUpServices(IServiceCollection services)
        {
            services.AddWpfBlazorWebView();
            services.AddSingleton<WeatherForecastService>();
            services.AddMudServices();
            services.AddScoped(sp => new HttpClient());

#if DEBUG
            services.AddBlazorWebViewDeveloperTools();
#endif
        }
    }
}
