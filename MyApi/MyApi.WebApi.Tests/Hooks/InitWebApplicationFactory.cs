﻿using Microsoft.AspNetCore.Mvc.Testing;
using TechTalk.SpecFlow;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.MsSql;

namespace MyApi.WebApi.Tests.Hooks;

using BoDi;
using Microsoft.Data.SqlClient;
using Respawn;

[Binding]
internal class InitWebApplicationFactory
{
    internal const string HttpClientKey = nameof(HttpClientKey);
    internal const string ApplicationKey = nameof(ApplicationKey);

    private MsSqlContainer _msSqlContainer = null!;

    [BeforeScenario]
    public async Task BeforeScenario(ScenarioContext scenarioContext, IObjectContainer objectContainer)
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
                    ReplaceDatabase(services, objectContainer);
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
                DbAdapter = DbAdapter.SqlServer
                
            });

        await respawner.ResetAsync(_msSqlContainer.GetConnectionString());
    }
}
