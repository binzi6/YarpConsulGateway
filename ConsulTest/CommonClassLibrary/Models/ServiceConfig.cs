using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonClassLibrary.Models
{
    public class ServiceConfig
    {
        public string ServiceName { get; set; } = "service-a";
        public string ServiceId { get; set; } = Guid.NewGuid().ToString();
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 5000;
        public string HealthCheckEndpoint { get; set; } = "/health";
        public int HealthCheckIntervalSeconds { get; set; } = 10;
        public string ConsulAddress { get; set; } = "http://localhost:1020";
    }

    public class ServiceInstance
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public int Weight { get; set; } = 1;
        public int Port { get; set; }
        public string[] Tags { get; set; } = Array.Empty<string>();
        public string HealthCheckUrl { get; set; } = string.Empty;
        public int ActiveConnections { get; set; } = 0;
        public double AvgResponseTime { get; set; } = 1.0;

        public string BaseUrl => $"http://{Address}:{Port}";
    }
}
