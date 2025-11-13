using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Consul;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

using YarpRouteConfig = Yarp.ReverseProxy.Configuration.RouteConfig;
using YarpClusterConfig = Yarp.ReverseProxy.Configuration.ClusterConfig;
using YarpDestinationConfig = Yarp.ReverseProxy.Configuration.DestinationConfig;
using YarpRouteMatch = Yarp.ReverseProxy.Configuration.RouteMatch;

/// <summary>
/// Consul 动态配置提供者
/// 从 Consul 获取健康的服务实例，并动态生成 YARP 路由与集群配置
/// </summary>
public class ConsulConfigProvider : IProxyConfigProvider, IDisposable
{
    private readonly ILogger<ConsulConfigProvider> _logger;
    private readonly object _lock = new();
    private ConsulProxyConfig _currentConfig;
    private Timer _timer;
    private bool _disposed;
    private readonly ConsulDiscoveryService _consulDiscoveryService;

    public ConsulConfigProvider(ILogger<ConsulConfigProvider> logger,
        ConsulDiscoveryService consulDiscoveryService)
    {
        _logger = logger;

        _consulDiscoveryService = consulDiscoveryService;
        // 初始化默认配置
        _currentConfig = new ConsulProxyConfig(Array.Empty<YarpRouteConfig>(), Array.Empty<ClusterConfig>(), new CancellationTokenSource());

        // 启动时立即加载一次配置（防止网关启动时没有路由）
        UpdateConfigAsync().GetAwaiter().GetResult();

        // 启动定时器，每 30 秒刷新一次服务列表（可根据需求调整）
        _timer = new Timer(async _ => await UpdateConfigAsync(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// 核心方法：从 Consul 拉取所有健康服务并更新 YARP 配置
    /// </summary>
    private async Task UpdateConfigAsync()
    {
        try
        {
            // 获取 Consul 中所有服务
            var allHealthyInstances = await _consulDiscoveryService.GetHealthyServicesAsync();
            // 按服务名分组
            var serviceGroups = allHealthyInstances
                .GroupBy(s => s.Name)
                .ToList();

            var routes = new List<YarpRouteConfig>();
            var clusters = new List<YarpClusterConfig>();

            foreach (var group in serviceGroups)
            {
                var serviceName = group.Key;
                var instances = group.ToList();

                // 构建 Cluster（目标服务节点）
                var destinations = instances.ToDictionary(
                    s => s.Id,
                    s => new YarpDestinationConfig
                    {
                        Address = $"http://{s.Address}:{s.Port}"
                    });

                var cluster = new YarpClusterConfig
                {
                    ClusterId = serviceName,
                    Destinations = destinations,
                    LoadBalancingPolicy = "PowerOfTwoChoices" // 负载均衡策略，可根据需求调整
                };
                clusters.Add(cluster);

                // 构建 Route（访问规则）
                var route = new YarpRouteConfig
                {
                    RouteId = $"{serviceName}-route",
                    ClusterId = serviceName,
                    Match = new YarpRouteMatch
                    {
                        Path = $"/{serviceName.ToLower()}/{{**catch-all}}"
                    },
                    Transforms = new[]
                    {
                        new Dictionary<string, string>
                        {
                            ["PathRemovePrefix"] = $"/{serviceName.ToLower()}"
                        }
                    }
                };
                routes.Add(route);
            }

            // 🔄 每次更新时重新创建新的 ChangeToken，确保 YARP 能刷新路由
            lock (_lock)
            {
                var cts = new CancellationTokenSource();
                _currentConfig = new ConsulProxyConfig(routes, clusters, cts);
            }

            _logger.LogInformation($"✅ Consul 动态路由刷新成功，共 {routes.Count} 条路由，{clusters.Count} 个集群。");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ 从 Consul 更新服务列表失败");
        }
    }

    /// <summary>
    /// 返回当前配置给 YARP 使用
    /// </summary>
    public IProxyConfig GetConfig() => _currentConfig;

    public void Dispose()
    {
        if (!_disposed)
        {
            _timer?.Dispose();
            _disposed = true;
        }
    }

    /// <summary>
    /// 自定义 ProxyConfig 实现，用于封装路由和集群信息
    /// </summary>
    private class ConsulProxyConfig : IProxyConfig
    {
        public ConsulProxyConfig(IReadOnlyList<YarpRouteConfig> routes, IReadOnlyList<YarpClusterConfig> clusters, CancellationTokenSource cts)
        {
            Routes = routes;
            Clusters = clusters;
            ChangeToken = new CancellationChangeToken(cts.Token);
        }

        public IReadOnlyList<YarpRouteConfig> Routes { get; }
        public IReadOnlyList<YarpClusterConfig> Clusters { get; }
        public IChangeToken ChangeToken { get; }
    }

}
