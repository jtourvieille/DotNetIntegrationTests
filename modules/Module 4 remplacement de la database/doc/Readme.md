# Module 4: remplacement de la database

## Création de la database

Pour commencer, nous allons créer une database dans le projet d'implémentation.

Dans une invite de commande de type powershell, lancer la commande suivante:

```sh
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=<YourStrong@Passw0rd>" -p 2024:1433 --name sql1 --hostname sql1 -d mcr.microsoft.com/mssql/server:2022-latest
```

Cela permettra de lancer un server sql dans un docker.

Ouvrir ensuite un SSMS sur cette instance:

![Connect](./img/ssms-connect.png)

Puis créer une database _Weather_:

![NewDb](./img/ssms-newdb.png)

![DbConf](./img/ssms-dbconf.png)

Créer ensuite la table _WeatherForecast_:

```sql
CREATE TABLE [dbo].[WeatherForecast](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Date] [date] NOT NULL,
	[TemperatureC] [int] NOT NULL,
	[Summary] [nvarchar](2000) NULL,
 CONSTRAINT [PK_WeatherForecast] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
```

Enfin, on va l'alimenter avec des données:

```sql
INSERT [dbo].[WeatherForecast] ([Date], [TemperatureC], [Summary]) VALUES (CAST(N'2024-01-01' AS Date), -5, N'Freezing')
GO
INSERT [dbo].[WeatherForecast] ([Date], [TemperatureC], [Summary]) VALUES (CAST(N'2024-07-01' AS Date), 25, N'Chilly')
GO
INSERT [dbo].[WeatherForecast] ([Date], [TemperatureC], [Summary]) VALUES (CAST(N'2024-08-15' AS Date), 35, N'Hot')
GO
```

## Utilisation de la database dans l'implémentation

Il faut maintenant se connecter à la base de données dans notre application.

On commence par référencer _EntityFramework_, qui sera notre ORM.

```
Microsoft.EntityFrameworkCore.SqlServer
```

Puis on ajoute la chaine de connexion dans le fichier _appsettings.json_:

```cs
"ConnectionStrings": {
  "WeatherContext": "Data Source=127.0.0.1,2024;Initial Catalog=Weather;User Id=Sa;Password=<YourStrong@Passw0rd>;Connect Timeout=30;Encrypt=False;TrustServerCertificate=False;ApplicationIntent=ReadWrite;MultiSubnetFailover=False"
}
```

Puis créer le _WeatherContext_:

```cs
using Microsoft.EntityFrameworkCore;

namespace MyApi.WebApi;

public class WeatherContext : DbContext
{
    public WeatherContext()
    {
    }

    public WeatherContext(DbContextOptions<WeatherContext> options)
        : base(options)
    {
    }

    public virtual DbSet<DbWeatherForecast> WeatherForecasts { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DbWeatherForecast>(entity =>
        {
            entity.ToTable("WeatherForecast");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Date).IsRequired();
            entity.Property(e => e.TemperatureC).IsRequired();
            entity.Property(e => e.Summary);
        });
    }
}

```

```cs
public class DbWeatherForecast
{
    public int Id { get; set; }

    public DateOnly Date { get; set; }

    public int TemperatureC { get; set; }

    public string? Summary { get; set; }
}

```

Ensuite, on peut le référencer dans notre démarrage d'application:

```cs
var connectionString = builder.Configuration.GetSection("ConnectionStrings")["WeatherContext"];

builder.Services.AddDbContext<WeatherContext>(options =>
    options.UseSqlServer(connectionString, providerOptions =>
    {
        providerOptions.EnableRetryOnFailure();
    }));
```

Il ne nous reste plus qu'à utiliser notre contexte dans le controller afin de renvoyer les données de la base.

```cs
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

```

## Utilisation de Testcontainers

Dans le projet de tests, référencer les packages

```
Microsoft.Data.SqlClient
Testcontainers.MsSql
```

On va ensuite créer un _MsSqlContainer_ pour héberger notre database:

```cs
private MsSqlContainer _msSqlContainer = null!;

[...]

_msSqlContainer = new MsSqlBuilder()
    .Build();

await _msSqlContainer.StartAsync();
```

Puis on va créer la méthode ReplaceDatabase:

```cs
private void ReplaceDatabase(IServiceCollection services)
{
    services.RemoveAll<DbContextOptions<WeatherContext>>();
    services.RemoveAll<WeatherContext>();

    services.AddDbContext<WeatherContext>(options =>
        options.UseSqlServer(_msSqlContainer.GetConnectionString(), providerOptions =>
        {
            providerOptions.EnableRetryOnFailure();
        }));
}
```

qu'on appellera à la suite du ReplaceLogging:

```cs
var application = new WebApplicationFactory<Program>()
    .WithWebHostBuilder(builder =>
    {
        builder.ConfigureTestServices(services =>
        {
            ReplaceLogging(services);
            ReplaceDatabase(services);
        });
    });
```

Enfin, on n'oublie pas de stopper & disposer le container une fois le scénario terminé:

```cs
await _msSqlContainer.StopAsync();
await _msSqlContainer.DisposeAsync().AsTask();
```

## Population de la base de données

On va introduire des données qui nous permettrons d'effectuer les tests. Pour cela, créer une méthode _PopulateDatabaseAsync_ comme suit:

```cs
private async Task PopulateDatabaseAsync()
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

            INSERT INTO [dbo].[WeatherForecast] ([Date], [TemperatureC], [Summary])
            VALUES
                ('2022-01-01T00:00:00Z', 25, 'Hot'),
                ('2022-01-02T00:00:00Z', 20, 'Warm'),
                ('2022-01-03T00:00:00Z', 15, 'Cool'),
                ('2022-01-04T00:00:00Z', 10, 'Cold'),
                ('2022-01-05T00:00:00Z', 5, 'Freezing');
        "
    };

    sqlConnection.Open();

    await sqlCommand.ExecuteNonQueryAsync();
}
```

Puis l'appeler dans la méthode _BeforeScenario_.

## Utilisation de Respawn

La database est créée. Ce qu'on veut désormais, c'est la retrouver dans le même état chaque fois qu'un scenario est joué, peu importe qu'il ajoute des données, les modifie ou les supprime. En effet, un test peut créer/modifier/supprimer des données en base, ce qui pertubera le test suivant. Ce que l'on souhaite, c'est connaître notre état de départ de la base.

Pour cela, nous allons utiliser la librairie Respawn.

Ajouter la référence suivante au projet de test

```
Respawn
```

Puis créer la méthode _InitializeRespawnAsync_ comme suit:

```cs
private async Task InitializeRespawnAsync()
{
    var respawner = await Respawner.CreateAsync(
        _msSqlContainer.GetConnectionString(),
        new()
        {
            DbAdapter = DbAdapter.SqlServer,
            TablesToIgnore = new Respawn.Graph.Table[] { "WeatherForecast" }
        });

    await respawner.ResetAsync(_msSqlContainer.GetConnectionString());
}
```

Méthode qu'il faudra évidemment appeler dans le _BeforeScenario_, après l'initialisation du container & le remplissage de la database.

Un repo contenant une solution est disponible [ici](https://github.com/jtourvieille/DotNetIntegrationTests/tree/main/modules/Module%204%20remplacement%20de%20la%20database/src/MyApi)

[< précédent](../../Module%203%20remplacement%20du%20système%20de%20log/doc/Readme.md) | [suivant >](../../Module%204bis%20remplacement%20de%20la%20database%20in%20memory/doc/Readme.md)
