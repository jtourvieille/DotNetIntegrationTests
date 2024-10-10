# Module 5: Ajout de tests

Démarrer avec le projet du module 4:

```
git clone https://github.com/jtourvieille/DotNetIntegrationTests.git --branch feature/module4
```

L'idée ici est de pouvoir ajouter une fonctionnalité permettant de requêter notre ressource _WeatherForecast_ par date.

Commençons par ajouter une première version d'un test

```
Scenario: Get weather forecast for one date with no forecast
	When I make a GET request to 'weatherforecast/2020-01-01'
	Then the response status code is '204'
```

Puis l'implémentation qui permet de répondre à ce cas de test:

```cs
[HttpGet]
[Route("{date}")]
public WeatherForecast? Get(DateOnly date)
{
    return null;
}
```

Enfin, il conviendrait d'adapter le SwaggerGen pour prendre en charge le type DateOnly, plutôt récent:

```cs
c => c.MapType<DateOnly>(() => new OpenApiSchema
{
    Type = "string",
    Format = "date"
})
```

Pour améliorer nos tests, on ne va plus initialiser les données en base en dur, mais depuis le test lui-même. Pour ce faire, on ajoute le background suivant dans le fichier de feature:

```
Background: 
	Given the existing forecast are
	| Date       | Summary  | TemperatureC |
	| 2023-01-01 | Freezing | -7           |
	| 2023-01-02 | Bracing  | 2            |
	| 2023-05-03 | Chilly   | 17           |
```

Pour nous permettre d'accéder à la base de données du côté tests, il nous faut l'enregistrer dans le conteneur IoC de _Specflow_. Pour ceci, il nous faut modifier la méthode _ReplaceDatabase_ comme suit:

```cs
private void ReplaceDatabase(IServiceCollection services, IObjectContainer objectContainer)
{
    services.RemoveAll<DbContextOptions<WeatherContext>>();
    services.RemoveAll<WeatherContext>();

    services.AddDbContext<WeatherContext>(options =>
        options.UseSqlServer(_msSqlContainer.GetConnectionString(), providerOptions =>
        {
            providerOptions.EnableRetryOnFailure();
        }));

    var database = new WeatherContext(new DbContextOptionsBuilder<WeatherContext>()
        .UseSqlServer(_msSqlContainer.GetConnectionString())
        .Options);

    objectContainer.RegisterInstanceAs(database);
}
```

Ceci nous permet de résoudre notre _WeatherContext_ dans le nouveau step que nous pouvons désormais implémenter:

```cs
[Given("the existing forecast are")]
public void GivenTheExistingWeatherForecastAre(Table table)
{
    var weatherContext = _objectContainer.Resolve<WeatherContext>();

    foreach (var row in table.Rows)
    {
        weatherContext.WeatherForecasts.Add(new DbWeatherForecast
        {
            Date = DateOnly.Parse(row["Date"]),
            TemperatureC = int.Parse(row["TemperatureC"]),
            Summary = row["Summary"]
        });
    }

    weatherContext.SaveChanges();
}
```

Enfin, __supprimer__ les données qu'on avait initialisé dans _InitWebApplicationFactory_ via la méthode _PopulateDatabaseAsync_:

```sql
INSERT INTO [dbo].[WeatherForecast] ([Date], [TemperatureC], [Summary])
VALUES
    ('2022-01-01T00:00:00Z', 25, 'Hot'),
    ('2022-01-02T00:00:00Z', 20, 'Warm'),
    ('2022-01-03T00:00:00Z', 15, 'Cool'),
    ('2022-01-04T00:00:00Z', 10, 'Cold'),
    ('2022-01-05T00:00:00Z', 5, 'Freezing');
```

ne pas oublier également de __supprimer__ l'exception pour la table dans la partie _Respawn_:

```cs
TablesToIgnore = new Respawn.Graph.Table[] { "WeatherForecast" }
```

En lancant le test, il devrait fonctionner.

On va maintenant ajouter un test qui permet de récupérer une prévision existante:

```
Scenario: Get weather forecast for one date with existing forecast
	When I make a GET request to 'weatherforecast/2023-01-02'
	Then the response status code is '200'
	And the response is
    | Date       | TemperatureC | Summary |
    | 2023-01-02 | 2            | Bracing |
```

et le code qui va avec:

```cs
[Then(@"the response is")]
public async Task ThenTheResponseIs(Table table)
{
    var response = await _scenarioContext.Get<HttpResponseMessage>(ResponseKey).Content.ReadAsStringAsync();

    var expected = table.CreateInstance<WeatherForecast>();
    var actual = JsonSerializer.Deserialize<WeatherForecast>(response, new JsonSerializerOptions
    {
        IgnoreReadOnlyProperties = true,
        PropertyNameCaseInsensitive = true
    });

    Assert.NotNull(actual);
    Assert.Equal(expected.Date, actual.Date);
    Assert.Equal(expected.TemperatureC, actual.TemperatureC);
    Assert.Equal(expected.TemperatureF, actual.TemperatureF);
    Assert.Equal(expected.Summary, actual.Summary);
}
```

Dans le controlleur, ajouter le code suivant:

```cs
[HttpGet]
[Route("{date}")]
public WeatherForecast? Get(DateOnly date)
{
    var dbWeather = _weatherContext.WeatherForecasts.FirstOrDefault(w => w.Date == date);

    if (dbWeather == null)
    {
        return null;
    }

    return new WeatherForecast
    {
        Date = dbWeather.Date,
        TemperatureC = dbWeather.TemperatureC,
        Summary = dbWeather.Summary
    };
}
```

Le test ne fonctionne pas encore tout à fait car ici, on manipule une _DateOnly_ qui n'a pas de _ValueRetriever_ associé (comme mentionné [ici](https://github.com/SpecFlowOSS/SpecFlow/tree/master/TechTalk.SpecFlow/Assist/ValueRetrievers)). Il nous faut donc créer un _DateOnlyValueRetriever.cs_:

```cs
public class DateOnlyValueRetriever : StructRetriever<DateOnly>
{
    /// <summary>
    /// Gets or sets the DateTimeStyles to use when parsing the string value.
    /// </summary>
    /// <remarks>Defaults to DateTimeStyles.None.</remarks>
    public static DateTimeStyles DateTimeStyles { get; set; } = DateTimeStyles.None;

    protected override DateOnly GetNonEmptyValue(string value)
    {
        DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles, out DateTime dateTimeValue);
        return DateOnly.FromDateTime(dateTimeValue);
    }
}

```

Enfin, on spécifie à Specflow qu'il doit utiliser ce nouveau retriever, à travers l'ajout d'une classe _CustomValueRetrievers_:

```cs
[Binding]
public static class CustomValueRetrievers
{
    [BeforeTestRun]
    public static void BeforeTestRun()
    {
        Service.Instance.ValueRetrievers.Register(new DateOnlyValueRetriever());
    }
}

```

Un repo contenant une solution est disponible ici

```
git clone https://github.com/jtourvieille/DotNetIntegrationTests.git --branch feature/module5
```

[< précédent](../../Module%204bis%20remplacement%20de%20la%20database%20in%20memory/doc/Readme.md) | [suivant >](../../Module%206%20optimisation/doc/Readme.md)
