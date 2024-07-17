namespace MyApi.WebApi.Tests.Features;

using Hooks;
using System.Net;
using TechTalk.SpecFlow;

[Binding]
internal class WeatherWebApiSteps
{
    private readonly ScenarioContext _scenarioContext;

    internal const string ResponseKey = nameof(ResponseKey);

    public WeatherWebApiSteps(ScenarioContext scenarioContext)
    {
        _scenarioContext = scenarioContext;
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
}
