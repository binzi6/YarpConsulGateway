using Microsoft.AspNetCore.Mvc;
using ServicesApi;

namespace ServicesApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<WeatherForecastController> _logger;

        public WeatherForecastController(ILogger<WeatherForecastController> logger)
        {
            _logger = logger;
        }

        [HttpGet(Name = "GetWeatherForecast")]
        public IEnumerable<WeatherForecast> Get()
        {
            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
        }


        [HttpGet, Route("Test")]
        public IActionResult Test()
        {
            //获取请求地址
            var request = HttpContext.Request;
            var headers = request.Headers;
            var host = request.Host;
            var path = request.Path;
            var queryString = request.QueryString;
            var method = request.Method;
            var contentType = request.ContentType;
            var cookies = request.Cookies;
            var scheme = request.Scheme;
            var connection = request.HttpContext.Connection;
            return new JsonResult(new
            {
                //获取电脑/容器名称
                machineName = Environment.MachineName,
                connection = new
                {
                    remoteIp = request.HttpContext.Connection.RemoteIpAddress?.ToString(),
                    localIp = request.HttpContext.Connection.LocalIpAddress?.ToString(),
                    remotePort = request.HttpContext.Connection.RemotePort,
                    localPort = HttpContext.Connection.LocalPort,
                },
                time = DateTime.Now.ToLocalTime()
            });
        }
    }
}
