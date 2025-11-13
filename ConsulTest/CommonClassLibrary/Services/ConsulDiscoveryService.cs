using Consul;
using CommonClassLibrary.Models;
using CommonClassLibrary.Services;
/// <summary>
/// Consul 服务发现客户端 - 不需要注册自己
/// </summary>
public class ConsulDiscoveryService : IDisposable
{
    private readonly IConsulClient _consulClient;
    private readonly LoadBalancer _loadBalancer;

    public ConsulDiscoveryService(IConsulClient consulClient)
    {
        _loadBalancer = new LoadBalancer();
        _consulClient = consulClient;
    }

    /// <summary>
    /// 获取所有健康的服务实例（纯发现，不注册）
    /// </summary>
    public async Task<List<ServiceInstance>> GetHealthyServicesAsync(string serviceName = "")
    {
        try
        {
            // Step 1: 获取所有服务名称（不含实例）
            var catalogServices = await _consulClient.Catalog.Services();
            var serviceNames = catalogServices.Response.Keys
            .Where(name => !string.IsNullOrEmpty(name)) // 过滤空服务名（Consul 有时会返回空 key）
            .ToList();

            var serviceList = new List<ServiceInstance>();
            if (string.IsNullOrEmpty(serviceName))
            {
                foreach (var name in serviceNames)
                {
                    if(name== "consul")
                        continue;

                    // 查询该服务的所有【健康】实例
                    var healthResponse = await _consulClient.Health.Service(name, CancellationToken.None);

                    var healthyInstances = healthResponse.Response;

                    if (healthyInstances.Length == 0)
                        continue; // 跳过无健康实例的服务

                    var servers = healthyInstances.Select(service => new ServiceInstance
                    {
                        Id = service.Service.ID,
                        Name = service.Service.Service,
                        Address = service.Service.Address,
                        Port = service.Service.Port,
                        Tags = service.Service.Tags ?? Array.Empty<string>(),
                        HealthCheckUrl = service.Checks.FirstOrDefault()?.ToString() ?? string.Empty
                    }).ToList();

                    //添加到list
                    serviceList.AddRange(servers);
                }
            }
            else
            {
                var queryResult = await _consulClient.Health.Service(serviceName, "", true);
                serviceList = queryResult.Response.Select(service => new ServiceInstance
                {
                    Id = service.Service.ID,
                    Name = service.Service.Service,
                    Address = service.Service.Address,
                    Port = service.Service.Port,
                    Tags = service.Service.Tags ?? Array.Empty<string>(),
                    HealthCheckUrl = service.Checks.FirstOrDefault()?.ToString() ?? string.Empty
                }).ToList();
            }

            Console.WriteLine($"🔍 发现 {serviceList.Count} 个健康的实例");
            return serviceList;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 获取服务列表失败: {ex.Message}");
            return new List<ServiceInstance>();
        }
    }

    /// <summary>
    /// 使用负载均衡获取下一个服务实例
    /// </summary>
    public async Task<ServiceInstance?> GetNextServiceAsync(string serviceName)
    {
        var services = await GetHealthyServicesAsync(serviceName);
        if (!services.Any())
            return null;

        return _loadBalancer.GetNext(services);
    }

    /// <summary>
    /// 检查服务是否健康
    /// </summary>
    public async Task<bool> IsServiceHealthyAsync(string serviceName)
    {
        var services = await GetHealthyServicesAsync(serviceName);
        return services.Any();
    }

    /// <summary>
    /// 调用服务的健康检查端点
    /// </summary>
    public async Task<bool> CallServiceHealthCheckAsync(ServiceInstance service)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync($"{service.BaseUrl}/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _consulClient?.Dispose();
    }
}