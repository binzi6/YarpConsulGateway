
using CommonClassLibrary.Services;
using Consul;
using Microsoft.AspNetCore.HttpLogging;
using Yarp.ReverseProxy.Configuration;

namespace GatewayApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 在构建时配置 URL
            builder.WebHost.UseUrls("http://0.0.0.0:8099");

            builder.Logging.AddConsole();

            // 注册 Consul 客户端
            builder.Services.AddSingleton<IConsulClient>(p => new ConsulClient(cfg =>
            {
                cfg.Address = new Uri(builder.Configuration["Consul:Address"] ?? "http://localhost:8500");
            }));

            // 注册 LoadBalancer（如果它是一个服务）
            builder.Services.AddSingleton<LoadBalancer>();

            // 注册 ConsulDiscoveryService
            builder.Services.AddSingleton<ConsulDiscoveryService>();

            // 注册自定义 Provider（YARP 会自动发现实现了 IProxyConfigProvider 的服务）
            builder.Services.AddSingleton<IProxyConfigProvider, ConsulConfigProvider>();

            //  不需要 LoadFromProvider —— 直接调用 AddReverseProxy()
            builder.Services.AddReverseProxy();

            // 启用 HTTP 日志（可选）
            builder.Services.AddHttpLogging(logging =>
            {
                logging.LoggingFields = HttpLoggingFields.RequestPath | HttpLoggingFields.ResponseStatusCode;
            });

            var app = builder.Build();

            app.UseHttpLogging();
            app.MapReverseProxy(); //这里会自动使用 ConsulConfigProvider

            app.MapGet("/", () => "Gateway is running.");

            app.Run();
        }
    }
}
