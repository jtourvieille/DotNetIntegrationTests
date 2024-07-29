using Microsoft.AspNetCore.Mvc;

namespace MyApi.WebApi.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    private readonly WeatherContext _weatherContext;

    public WeatherForecastController(WeatherContext weatherContext)
    {
        _weatherContext = weatherContext;
    }

    [HttpGet(Name = "GetWeatherForecast")]
    public IEnumerable<WeatherForecast> Get()
    {
        var allWeathers = _weatherContext.WeatherForecasts.ToList();

        return allWeathers.Select(dbWeather => new WeatherForecast
        {
            Date = dbWeather.Date,
            TemperatureC = dbWeather.TemperatureC,
            Summary = dbWeather.Summary
        });
    }
}
