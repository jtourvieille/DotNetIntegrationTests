# Module 5: Ajout de tests

On repart des sources du module 4 [ici](https://github.com/jtourvieille/DotNetIntegrationTests/tree/main/modules/Module%204%20remplacement%20de%20la%20database/src/MyApi) pour la solution.

L'idée ici est de pouvoir ajouter une fonctionnalité permettant de requêter notre ressource WeatherForecast par date.

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

Et bien prendre soin de supprimer le nom de la première route.

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

Pour nous permettre d'accéder à la base de données du côté tests, il nous faut l'enregistrer dans le conteneur IoC de Specflow. Pour ceci, il nous faut modifier la méthode ReplaceDatabase comme suit:

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

Ceci nous permet de résoudre notre WeatherContext dans le nouveau step que nous pouvons désormais implémenter:

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

Enfin, __supprimer__ les données qu'on avait initialisé dans InitWebApplicationFactory via la méthode PopulateDatabaseAsync:

```sql
INSERT INTO [dbo].[WeatherForecast] ([Date], [TemperatureC], [Summary])
VALUES
    ('2022-01-01T00:00:00Z', 25, 'Hot'),
    ('2022-01-02T00:00:00Z', 20, 'Warm'),
    ('2022-01-03T00:00:00Z', 15, 'Cool'),
    ('2022-01-04T00:00:00Z', 10, 'Cold'),
    ('2022-01-05T00:00:00Z', 5, 'Freezing');
```

ne pas oublier également de __supprimer__ l'exception pour la table dans la partie Respawn:

```cs
TablesToIgnore = new Respawn.Graph.Table[] { "WeatherForecast" }
```

En lancant le test, il devrait fonctionner.

On va maintenant ajouter un test qui permet de récupérer une prévision existante:

```
Scenario: Get weather forecast for one date with existing forecast
	When I make a GET request to 'weatherforecast/2023-01-02'
	Then the response status code is '200'
	And the response body is
		"""
		{
			"date": "2023-01-02",
			"summary": "Bracing",
			"temperatureC": 2
		}
		"""
```

et le code qui va avec:

```cs
[Then(@"the response body is")]
public async Task ThenTheResponseBodyIs(string expectedJsonBody)
{
    var response = await _scenarioContext.Get<HttpResponseMessage>(ResponseKey).Content.ReadAsStringAsync();

    var expected = JsonSerializer.Deserialize<WeatherForecast>(expectedJsonBody);
    var actual = JsonSerializer.Deserialize<WeatherForecast>(response);

    Assert.Equal(expected, actual);
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

Un repo contenant une solution est disponible [ici](https://github.com/jtourvieille/DotNetIntegrationTests/tree/main/modules/Module%205%20ajout%20de%20tests/src/MyApi)

[< précédent](../../Module%204bis%20remplacement%20de%20la%20database%20in%20memory/doc/Readme.md)
