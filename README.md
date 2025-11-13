# 🧭 YarpConsulGateway

一个基于 **.NET 8 + YARP + Consul** 的智能网关服务，支持**自动服务发现、动态路由刷新与负载均衡**。  
通过集成 Consul 健康检查，网关可实时感知后端服务变化，无需手动修改配置。

---

## 🚀 功能特性

✅ **Consul 自动发现**  
自动从 Consul 注册中心读取健康服务实例。

✅ **动态路由生成**  
YARP 路由配置由 Consul 服务自动构建，无需手工编写。

✅ **负载均衡策略支持**  
支持 YARP 内置策略，如 `RoundRobin`、`PowerOfTwoChoices` 等。

✅ **Consul 集群兼容**  
支持多个 Consul 节点（例如 `consul1/2/3`），实现高可用注册中心。

✅ **轻量级部署**  
通过 Docker Compose 一键启动 Consul 集群 + 网关。

---

## 🧩 项目结构
YarpConsulGateway/
├─ GatewayApi/ # 网关主工程
│ ├─ Program.cs # 程序入口，注册服务与 YARP
│ ├─ ConsulConfigProvider.cs # 从 Consul 动态生成 YARP 配置
│ ├─ ConsulDiscoveryService.cs# Consul 服务发现封装
│ ├─ LoadBalancer.cs # 自定义负载均衡逻辑（可选）
│ └─ appsettings.json # 网关配置文件
│
├─ docker-compose.yml # Consul 多节点集群部署
├─ server1/consul.json # Consul 节点1配置
├─ server2/consul.json # Consul 节点2配置
├─ server3/consul.json # Consul 节点3配置
└─ README.md # 项目说明


---

## 🧠 架构概览

+-----------------------+
| YARP Gateway |
| (YarpConsulGateway) |
+----------+------------+
|
| 自动发现、健康检查
v
+-----------------------+
| Consul 集群 |
| consul1 / consul2 / consul3 |
+-----------------------+
|
| 注册服务
v
+-----------------------+
| Service-A / B / C |
| .NET / Java / Node |
+-----------------------+


---

## ⚙️ 环境要求

- .NET 8 SDK 或更高版本  
- Docker / Docker Compose  
- Visual Studio 2022 或 VS Code  

---

## 🧭 快速启动

### 1️⃣ 启动 Consul 集群
```bash
docker-compose up -d


启动完成后，访问以下地址查看 Consul 控制台：

http://localhost:1027
 → consul1

http://localhost:1028
 → consul2

http://localhost:1029
 → consul3

🧩 可通过 http://localhost:1027/v1/catalog/services 查看注册的服务。

2️⃣ 启动网关

在 GatewayApi 目录下执行：

dotnet run


默认网关监听：

http://localhost:8099


当有服务在 Consul 注册时，可直接访问：

http://localhost:8099/service-a/health


网关将自动转发到对应服务的健康检查地址。

⚙️ 配置说明

appsettings.json 示例：

{
  "Consul": {
    "Address": "http://localhost:1027"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}


修改 Consul:Address 可切换到任意节点。

🧩 关键类说明
类名	作用
ConsulConfigProvider	实现 IProxyConfigProvider，动态生成路由与集群配置
ConsulDiscoveryService	统一封装 Consul 健康检查与服务获取逻辑
LoadBalancer	自定义负载均衡策略（可选）
🧪 示例：Consul 注册服务

注册一个测试服务到 Consul：

curl --request PUT \
  --data '{"ID":"service-a-1","Name":"service-a","Address":"host.docker.internal","Port":8090,"Check":{"HTTP":"http://host.docker.internal:8090/health","Interval":"10s"}}' \
  http://localhost:1027/v1/agent/service/register


YARP 网关会在 30 秒内自动生成对应路由 /service-a/{**catch-all}。

🧱 开发计划（Roadmap）

 支持服务标签自动路由规则

 支持自定义中间件注入（Auth、RateLimit）

 提供可视化 Dashboard 查看动态路由

 支持 Nacos / Eureka 替代 Consul 的版本

📄 License

MIT License
Copyright © 2025

🌟 Star 一下

如果这个项目对你有帮助，请给一个 ⭐ 支持一下，谢谢！
