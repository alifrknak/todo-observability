using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace todo.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{

    static int statusCodeCounter = 0;
        
    [HttpGet(Name = "GetWeatherForecast")]
    public IActionResult Get()
    {
        Thread.Sleep(Random.Shared.Next(1000));

        statusCodeCounter++;
        if (statusCodeCounter % 30 == 0)
        {
            return StatusCode(500);
        }
        if (statusCodeCounter % 20 == 0)
        {
            return StatusCode(400);
        }
        return StatusCode(200);


    }
}
