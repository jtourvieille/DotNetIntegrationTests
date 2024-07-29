using BoDi;
using Microsoft.EntityFrameworkCore;
using TechTalk.SpecFlow;

namespace MyApi.WebApi.Tests.Hooks;

[Binding]
internal sealed class InMemoryDatabase
{
    [BeforeScenario]
    public static void BeforeScenario(IObjectContainer objectContainer)
    {
        var scchContextOptions = new DbContextOptionsBuilder<WeatherContext>()
            .UseInMemoryDatabase("TestingWeatherContext")
            .Options;

        var database = new WeatherContext(scchContextOptions);
        database.Database.EnsureDeleted();
        objectContainer.RegisterInstanceAs(database);
    }

    [AfterScenario]
    public static void AfterScenario(IObjectContainer objectContainer)
    {
        WeatherContext? database = objectContainer.Resolve<WeatherContext>();

        if (database is not null)
        {
            database.Database.EnsureDeleted();
        }
    }
}
