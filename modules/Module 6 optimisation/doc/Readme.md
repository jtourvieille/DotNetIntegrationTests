# Module 6: Optimisation

Démarrer avec le projet du module 5:

```
git clone https://github.com/jtourvieille/DotNetIntegrationTests.git --branch feature/module5
```

Jusqu'à présent, nous avons 3 tests:

```
Scenario: Get weather forecasts
Scenario: Get weather forecast for one date with no forecast
Scenario: Get weather forecast for one date with existing forecast
```

Pour chacun d'eux, on va poper un test container et initialiser une database. L'utilité de Respawn dans ces conditions est discutable. De plus, la lenteur des tests se fait ressentir, puisqu'initiliser un test contanier pour chaque scénario n'est pas neutre en terme de ressources.

On peut cependant optimiser notre processus: on pourrait poper le test container au démarrage, exécuter l'ensemble des tests, en remettant la database à zéro entre chaque test, puis décharger le test container en fin de run.

## Ajout d'un test non idempotent

Commençons par ajouter un test permettant d'ajouter une entrée WeatherForecast:

```
Scenario: Save weather forecast
	Given the weather forecast
	| Date       | TemperatureC | Summary |
	| 2023-01-05 | 5            | Bracing |
	When I save it
	Then the response status code is '200'
```

Ensuite, complétons la logique associée:

```cs
[Given("the weather forecast")]
public void GivenTheWeatherForecast(Table table)
{
    var row = table.Rows[0];

    var forecast = new WeatherForecast
    {
        Date = DateOnly.Parse(row["Date"]),
        TemperatureC = int.Parse(row["TemperatureC"]),
        Summary = row["Summary"]
    };

    _scenarioContext.Add(ForecastKey, forecast);
}

[When("I save it")]
public async Task WhenISaveIt()
{
    var client = _scenarioContext.Get<HttpClient>(InitWebApplicationFactory.HttpClientKey);

    var weatherForecast = _scenarioContext.Get<WeatherForecast>(ForecastKey);

    var stringContent = new StringContent(
        JsonConvert.SerializeObject(weatherForecast),
        Encoding.UTF8,
        "application/json");

    _scenarioContext.Add(ResponseKey, await client.PostAsync("weatherforecast", stringContent));
}
```

Puis implémentons la logique côté code applicatif:

```
[HttpPost]
public IActionResult Post([FromBody] WeatherForecast weatherForecast)
{
    _weatherContext.WeatherForecasts.Add(new DbWeatherForecast
    {
        Date = weatherForecast.Date,
        TemperatureC = weatherForecast.TemperatureC,
        Summary = weatherForecast.Summary
    });

    _weatherContext.SaveChanges();

    return Ok();
}
```

## DatabaseHook

Et maintenant, pour appliquer notre nouvelle stratégie, nous allons créer un DatabaseHook afin de déplacer la logique d'initialisation de la database:

```cs
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;
using TechTalk.SpecFlow;

namespace MyApi.WebApi.Tests.Hooks;

[Binding]
public class DatabaseHook
{
    private static MsSqlContainer _msSqlContainer = null!;

    public static MsSqlContainer MsSqlContainer => _msSqlContainer;

    [BeforeTestRun]
    public static void BeforeTestRun()
    {
        Task.Run(async () =>
        {
            await InitializeMsSqlContainerAsync();
            await PopulateDatabaseAsync();
        }).GetAwaiter().GetResult();
    }


    [AfterTestRun]
    public static void AfterTestRun()
    {
        Task.Run(async () =>
        {
            await _msSqlContainer.StopAsync();
            await _msSqlContainer.DisposeAsync().AsTask();
        }).GetAwaiter().GetResult();
    }

    private static async Task InitializeMsSqlContainerAsync()
    {
        _msSqlContainer = new MsSqlBuilder().Build();

        await _msSqlContainer.StartAsync();
    }

    private static async Task PopulateDatabaseAsync()
    {
        await using SqlConnection sqlConnection = new SqlConnection(_msSqlContainer.GetConnectionString());

        await using var sqlCommand = new SqlCommand
        {
            Connection = sqlConnection,
            CommandText = @"
            CREATE TABLE [dbo].[WeatherForecast] (
                [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                [Date] DATE NOT NULL,
                [TemperatureC] INT NOT NULL,
                [Summary] NVARCHAR(2000) NULL
            );
        "
        };

        sqlConnection.Open();

        await sqlCommand.ExecuteNonQueryAsync();
    }
}
```

Un repo contenant une solution est disponible ici

```
git clone https://github.com/jtourvieille/DotNetIntegrationTests.git --branch feature/module6
```

[< précédent](../../Module%205%20ajout%20de%20tests/doc/Readme.md)
