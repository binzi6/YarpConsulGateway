using Consul;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;
public class ConsulService : IHostedService
{
    private readonly IConsulClient _consulClient;
    private string _registrationID;
    private readonly IConfiguration _configuration;
    public ConsulService(IConsulClient consulClient,
        IConfiguration configuration)
    {
        _consulClient = consulClient;
        _configuration = configuration;
    }
    /// <summary>
    /// 异步启动服务并注册到Consul
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务</returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // 创建服务注册信息
        var registration = new AgentServiceRegistration
        {
            // 生成唯一服务ID
            ID = $"{_configuration["Consul:Name"]}-{Guid.NewGuid()}",
            // 设置服务名称
            Name = _configuration["Consul:Name"],
            // 设置服务地址
            Address = _configuration["Consul:IP"],
            // 设置服务端口
            Port = Convert.ToInt32(_configuration["Consul:Port"]),
            // 配置健康检查
            Check = new AgentServiceCheck
            {
                // 设置健康检查的HTTP地址
                HTTP = $"http://{_configuration["Consul:IP"]}:{Convert.ToInt32(_configuration["Consul:Port"])}/health",
                // 设置健康检查间隔时间
                Interval = TimeSpan.FromSeconds(10),
                // 设置健康检查超时时间
                Timeout = TimeSpan.FromSeconds(5),
                // 设置健康检查失败后注销服务的时间 必须大于 Interval，否则服务没有重试机会就被注销了
                DeregisterCriticalServiceAfter = TimeSpan.FromMinutes(1)
            }
        };
        // 保存注册ID
        _registrationID = registration.ID;
        // 将服务注册到Consul
        await _consulClient.Agent.ServiceRegister(registration, cancellationToken);
    }

    /// <summary>
    /// 异步停止服务并从Consul注销
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示异步操作的任务</returns>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // 从Consul注销服务
        await _consulClient.Agent.ServiceDeregister(_registrationID, cancellationToken);
    }

}
