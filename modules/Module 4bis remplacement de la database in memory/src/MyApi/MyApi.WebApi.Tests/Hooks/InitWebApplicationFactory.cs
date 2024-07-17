using Microsoft.AspNetCore.Mvc.Testing;
using TechTalk.SpecFlow;

namespace MyApi.WebApi.Tests.Hooks;

using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

[Binding]
internal class InitWebApplicationFactory
{
    internal const string HttpClientKey = nameof(HttpClientKey);
    internal const string ApplicationKey = nameof(ApplicationKey);

    [BeforeScenario]
    public void BeforeScenario(ScenarioContext scenarioContext)
    {
        var application = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
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
    public void AfterScenario(ScenarioContext scenarioContext)
    {
        if (scenarioContext.TryGetValue(HttpClientKey, out var client) && client is IDisposable disposable)
        {
            disposable.Dispose();
        }

        if (scenarioContext.TryGetValue(ApplicationKey, out var application) && application is IDisposable disposableApplication)
        {
            disposableApplication.Dispose();
        }
    }

    private static void ReplaceLogging(IServiceCollection services)
    {
        services.RemoveAll(typeof(ILogger<>));
        services.RemoveAll<ILogger>();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
    }

    private static void ReplaceDatabase(IServiceCollection services)
    {
        services.RemoveAll<DbContextOptions<WeatherContext>>();
        services.RemoveAll<WeatherContext>();

        services.AddDbContext<WeatherContext>(options =>
            options.UseInMemoryDatabase("TestingWeatherContext"));
    }
}
