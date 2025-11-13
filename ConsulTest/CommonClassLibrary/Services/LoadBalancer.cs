using CommonClassLibrary.Models;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace CommonClassLibrary.Services
{
    /// <summary>
    /// 负载均衡策略接口
    /// 定义了所有负载均衡策略必须实现的方法
    /// </summary>
    public interface ILoadBalancingStrategy
    {
        /// <summary>
        /// 从服务列表中选择一个服务实例
        /// </summary>
        /// <param name="services">可用的服务实例列表</param>
        /// <param name="key">用于一致性哈希的键值（可选）</param>
        /// <returns>选中的服务实例</returns>
        ServiceInstance Select(List<ServiceInstance> services, string? key = null);
    }

    #region 策略实现
    /// <summary>
    /// 随机策略实现
    /// 从服务列表中随机选择一个服务实例
    /// </summary>
    public class RandomStrategy : ILoadBalancingStrategy
    {
        /// <summary>
        /// 随机选择一个服务实例
        /// </summary>
        /// <param name="services">可用的服务实例列表</param>
        /// <param name="key">未使用（保留参数）</param>
        /// <returns>随机选中的服务实例</returns>
        public ServiceInstance Select(List<ServiceInstance> services, string? key = null)
        {
            if (services == null || services.Count == 0)
                throw new ArgumentException("Service list is empty");
            return services[Random.Shared.Next(services.Count)];
        }
    }

    /// <summary>
    /// 轮询策略实现
    /// 按顺序循环选择服务实例
    /// </summary>
    public class RoundRobinStrategy : ILoadBalancingStrategy
    {
        private int _index = -1;

        /// <summary>
        /// 按轮询顺序选择下一个服务实例
        /// 使用原子操作确保线程安全
        /// </summary>
        /// <param name="services">可用的服务实例列表</param>
        /// <param name="key">未使用（保留参数）</param>
        /// <returns>下一个轮询到的服务实例</returns>
        public ServiceInstance Select(List<ServiceInstance> services, string? key = null)
        {
            if (services == null || services.Count == 0)
                throw new ArgumentException("Service list is empty");

            int idx = Interlocked.Increment(ref _index);
            return services[idx % services.Count];
        }
    }

    /// <summary>
    /// 加权轮询策略实现
    /// 根据服务实例的权重值进行选择，权重越高的实例被选中的概率越大
    /// </summary>
    public class WeightedRoundRobinStrategy : ILoadBalancingStrategy
    {
        /// <summary>
        /// 根据权重值选择服务实例
        /// 实现方式：计算总权重，生成随机数，根据随机数落在的权重区间选择对应实例
        /// </summary>
        /// <param name="services">可用的服务实例列表</param>
        /// <param name="key">未使用（保留参数）</param>
        /// <returns>根据权重选中的服务实例</returns>
        public ServiceInstance Select(List<ServiceInstance> services, string? key = null)
        {
            if (services == null || services.Count == 0)
                throw new ArgumentException("Service list is empty");

            int totalWeight = services.Sum(s => s.Weight);
            int rand = Random.Shared.Next(totalWeight);

            foreach (var s in services)
            {
                if (rand < s.Weight)
                    return s;
                rand -= s.Weight;
            }

            return services.Last();
        }
    }

    /// <summary>
    /// 最少连接策略实现
    /// 选择当前活动连接数最少的服务实例
    /// </summary>
    public class LeastConnectionStrategy : ILoadBalancingStrategy
    {
        /// <summary>
        /// 选择活动连接数最少的服务实例
        /// </summary>
        /// <param name="services">可用的服务实例列表</param>
        /// <param name="key">未使用（保留参数）</param>
        /// <returns>活动连接数最少的服务实例</returns>
        public ServiceInstance Select(List<ServiceInstance> services, string? key = null)
        {
            if (services == null || services.Count == 0)
                throw new ArgumentException("Service list is empty");

            return services.OrderBy(s => s.ActiveConnections).First();
        }
    }

    /// <summary>
    /// 一致性哈希策略实现（简化版）
    /// 根据键的哈希值选择服务实例，相同键总是映射到同一实例
    /// </summary>
    public class ConsistentHashStrategy : ILoadBalancingStrategy
    {
        /// <summary>
        /// 根据键的哈希值选择服务实例
        /// 如果未提供键，则使用随机生成的Guid
        /// </summary>
        /// <param name="services">可用的服务实例列表</param>
        /// <param name="key">用于哈希计算的键值</param>
        /// <returns>根据哈希值选中的服务实例</returns>
        public ServiceInstance Select(List<ServiceInstance> services, string? key = null)
        {
            if (services == null || services.Count == 0)
                throw new ArgumentException("Service list is empty");

            if (string.IsNullOrEmpty(key))
                key = Guid.NewGuid().ToString();

            int hash = key.GetHashCode();
            int index = Math.Abs(hash) % services.Count;
            return services[index];
        }
    }

    /// <summary>
    /// 响应时间加权策略实现（动态负载均衡）
    /// 根据服务实例的平均响应时间进行选择，响应时间越短被选中的概率越大
    /// </summary>
    public class WeightedResponseTimeStrategy : ILoadBalancingStrategy
    {
        /// <summary>
        /// 根据平均响应时间选择服务实例
        /// 实现方式：计算响应时间的倒数作为权重，按权重比例随机选择
        /// </summary>
        /// <param name="services">可用的服务实例列表</param>
        /// <param name="key">未使用（保留参数）</param>
        /// <returns>根据响应时间选中的服务实例</returns>
        public ServiceInstance Select(List<ServiceInstance> services, string? key = null)
        {
            if (services == null || services.Count == 0)
                throw new ArgumentException("Service list is empty");

            double total = services.Sum(s => 1.0 / s.AvgResponseTime);
            double rand = Random.Shared.NextDouble() * total;
            double cumulative = 0;

            foreach (var s in services)
            {
                cumulative += 1.0 / s.AvgResponseTime;
                if (rand <= cumulative)
                    return s;
            }

            return services.Last();
        }
    }
    #endregion

    // -------------------- 负载均衡器主类 --------------------

    /// <summary>
    /// 负载均衡类型枚举
    /// 定义了系统支持的所有负载均衡策略
    /// </summary>
    public enum LoadBalanceType
    {
        Random,                    // 随机策略
        RoundRobin,                // 轮询策略
        WeightedRoundRobin,        // 加权轮询策略
        LeastConnection,           // 最少连接策略
        ConsistentHash,            // 一致性哈希策略
        WeightedResponseTime       // 响应时间加权策略
    }

    /// <summary>
    /// 主负载均衡器类
    /// 管理所有负载均衡策略，提供统一的负载均衡入口
    /// </summary>
    public class LoadBalancer
    {
        /// <summary>
        /// 存储负载均衡策略的字典
        /// 使用并发字典确保线程安全
        /// </summary>
        private readonly ConcurrentDictionary<LoadBalanceType, ILoadBalancingStrategy> _strategies = new();

        /// <summary>
        /// 构造函数，初始化所有支持的负载均衡策略
        /// </summary>
        public LoadBalancer()
        {
            _strategies[LoadBalanceType.Random] = new RandomStrategy();
            _strategies[LoadBalanceType.RoundRobin] = new RoundRobinStrategy();
            _strategies[LoadBalanceType.WeightedRoundRobin] = new WeightedRoundRobinStrategy();
            _strategies[LoadBalanceType.LeastConnection] = new LeastConnectionStrategy();
            _strategies[LoadBalanceType.ConsistentHash] = new ConsistentHashStrategy();
            _strategies[LoadBalanceType.WeightedResponseTime] = new WeightedResponseTimeStrategy();
        }

        /// <summary>
        /// 获取下一个服务实例
        /// </summary>
        /// <param name="services">可用的服务实例列表</param>
        /// <param name="type">负载均衡策略类型</param>
        /// <param name="key">用于一致性哈希的键值（可选）</param>
        /// <returns>根据指定策略选中的服务实例</returns>
        /// <exception cref="NotSupportedException">当指定的负载均衡类型不支持时抛出</exception>
        public ServiceInstance GetNext(List<ServiceInstance> services, LoadBalanceType type = LoadBalanceType.RoundRobin, string? key = null)
        {
            if (!_strategies.TryGetValue(type, out var strategy))
                throw new NotSupportedException($"Load balance type {type} not supported.");

            return strategy.Select(services, key);
        }
    }
}
