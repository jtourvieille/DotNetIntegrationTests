﻿namespace MyApi.WebApi.Tests.Features;

using Hooks;
using System.Net;
using System.Text.Json;
using BoDi;
using TechTalk.SpecFlow;
using TechTalk.SpecFlow.Assist;

[Binding]
internal class WeatherWebApiSteps
{
    private readonly ScenarioContext _scenarioContext;
    private readonly IObjectContainer _objectContainer;

    internal const string ResponseKey = nameof(ResponseKey);

    public WeatherWebApiSteps(ScenarioContext scenarioContext, IObjectContainer objectContainer)
    {
        _scenarioContext = scenarioContext;
        _objectContainer = objectContainer;
    }

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

    [When("I make a GET request to '(.*)'")]
    public async Task WhenIMakeAGetRequestTo(string endpoint)
    {
        var client = _scenarioContext.Get<HttpClient>(InitWebApplicationFactory.HttpClientKey);
        _scenarioContext.Add(ResponseKey, await client.GetAsync(endpoint));
    }

    [Then(@"the response status code is '(.*)'")]
    public void ThenTheResponseStatusCodeIs(int statusCode)
    {
        var expected = (HttpStatusCode)statusCode;
        Assert.Equal(expected, _scenarioContext.Get<HttpResponseMessage>(ResponseKey).StatusCode);
    }

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
}
