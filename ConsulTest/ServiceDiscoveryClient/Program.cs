using Consul;

namespace ServiceDiscoveryClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // 创建纯客户端 - 不注册自己！
            using var consulClient = new ConsulDiscoveryService(new ConsulClient(cfg =>
            {
                cfg.Address = new Uri("http://localhost:1020");
            }));

            // 演示服务发现和负载均衡
            await DemonstrateServiceDiscovery(consulClient);

            Console.WriteLine("🚀 服务发现客户端运行完成");
        }

        static async Task DemonstrateServiceDiscovery(ConsulDiscoveryService consulClient)
        {
            Console.WriteLine("🎯 开始服务发现演示...");

            // 1. 检查服务健康状态
            var isHealthy = await consulClient.IsServiceHealthyAsync("service-a");
            Console.WriteLine($"📊 service-a 健康状态: {(isHealthy ? "健康" : "不健康")}");

            if (!isHealthy)
            {
                Console.WriteLine("❌ 没有发现健康的 service-a 实例");
                //等待输入
                Console.ReadKey();
                return;
            }

            // 2. 获取所有健康实例
            var services = await consulClient.GetHealthyServicesAsync("service-a");
            Console.WriteLine("\n📋 所有健康实例:");
            foreach (var service in services)
            {
                Console.WriteLine($"   - {service.Name} ({service.Address}:{service.Port})");
            }

            // 3. 演示负载均衡调用
            Console.WriteLine("\n🔄 负载均衡演示:");
            for (int i = 1; i <= 10; i++)
            {
                var service = await consulClient.GetNextServiceAsync("service-a");
                if (service == null)
                {
                    Console.WriteLine($"   {i}. 无可用服务实例");
                    continue;
                }

                // 直接调用服务（不通过Consul）
                try
                {
                    using var httpClient = new HttpClient();
                    var response = await httpClient.GetAsync($"{service.BaseUrl}/health");
                    Console.WriteLine($"   {i}. 调用 {service.Address}:{service.Port} - 状态: {response.StatusCode}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   {i}. 调用 {service.Address}:{service.Port} 失败: {ex.Message}");
                }

                await Task.Delay(1000);
            }
        }
    }
}
