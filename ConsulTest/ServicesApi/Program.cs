using Consul;
using Microsoft.AspNetCore.HostFiltering;

var builder = WebApplication.CreateBuilder(args);


// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 注册 Consul 服务客户端为单例模式
// IConsulClient 是接口，ConsulClient 是实现类
// 使用依赖注入方式注册，确保整个应用程序生命周期内只有一个 ConsulClient 实例
//builder.Services.AddSingleton<IConsulClient, ConsulClient>(p => new ConsulClient(cfg =>
//{
//    cfg.Address = new Uri("http://localhost:1020");
//}));
builder.Services.AddSingleton<IConsulClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var address = config["Consul:Address"] ?? "http://localhost:8500";
    return new ConsulClient(cfg => cfg.Address = new Uri(address));
});
//builder.WebHost.UseUrls("http://*:8090");
// 将ConsulService注册为托管服务
// 这意味着ConsulService将作为后台服务在应用程序启动时自动启动
// 托管服务通常用于执行长期运行的任务，如后台处理、定时任务等
builder.Services.AddHostedService<ConsulService>();
// 配置 Kestrel 服务器监听 HTTP 和 HTTPS 端口
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080); // HTTP
    //options.ListenAnyIP(8081, configure => configure.UseHttps()); // HTTPS
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// 配置健康检查端点路由
// 当GET请求发送到"/health"路径时，返回"OK"字符串
app.MapGet("/health", () => "OK");

app.Run();

