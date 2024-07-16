using Microsoft.AspNetCore.Mvc.Testing;
using TechTalk.SpecFlow;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.MsSql;

namespace MyApi.WebApi.Tests.Hooks;

using Microsoft.Data.SqlClient;
using Respawn;

[Binding]
internal class InitWebApplicationFactory
{
    internal const string HttpClientKey = nameof(HttpClientKey);
    internal const string ApplicationKey = nameof(ApplicationKey);

    private MsSqlContainer _msSqlContainer = null!;

    [BeforeScenario]
    public async Task BeforeScenario(ScenarioContext scenarioContext)
    {
        await InitializeMsSqlContainerAsync();

        await PopulateDatabaseAsync();

        await InitializeRespawnAsync();

        var application = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    ReplaceLogging(services);
                    ReplaceDatabase(services);
                });
            });

        var client = application.CreateClient();

        scenarioContext.TryAdd(HttpClientKey, client);
        scenarioContext.TryAdd(ApplicationKey, application);
    }

    [AfterScenario]
    public async Task AfterScenario(ScenarioContext scenarioContext)
    {
        if (scenarioContext.TryGetValue(HttpClientKey, out var client) && client is IDisposable disposable)
        {
            disposable.Dispose();
        }

        if (scenarioContext.TryGetValue(ApplicationKey, out var application) && application is IDisposable disposableApplication)
        {
            disposableApplication.Dispose();
        }

        await _msSqlContainer.StopAsync();
        await _msSqlContainer.DisposeAsync().AsTask();
    }

    private static void ReplaceLogging(IServiceCollection services)
    {
        services.RemoveAll(typeof(ILogger<>));
        services.RemoveAll<ILogger>();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
    }

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

    private async Task InitializeMsSqlContainerAsync()
    {
        _msSqlContainer = new MsSqlBuilder().Build();

        await _msSqlContainer.StartAsync();
    }

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
}
