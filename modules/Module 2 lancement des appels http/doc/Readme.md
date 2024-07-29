# Module 2: lancement des appels http

Démarrer avec le projet du module précédent:

```
git clone https://github.com/jtourvieille/DotNetIntegrationTests.git --branch feature/module1
```

Ajouter un projet de test xUnit ainsi qu'une référence à une librairie _SpecFlow.xUnit_, comme mentionné au [module 1](./../../Module%201%20création%20du%20projet%20de%20test/doc/Readme.md)

## Ajout de la WebApplicationFactory

Ajouter la classe qui va permettre de démarrer notre application:

```
Hooks/InitWebApplicationFactory.cs
```

Ajouter ensuite l'attribut _BeforeScenario_ pour pouvoir exécuter du code avant le démarrage d'un scénario Gherkin. Ceci va nous permettre de démarrer notre serveur afin de pouvoir le requêter par la suite.

Commencer par ajouter une référence vers le projet web:

![refwebproject](img/refwebproject.png)

Enfin, ajouter la librairie de test _Microsoft.AspNetCore.Mvc.Testing_.

## Visiblité du de la classe Program

Pour que la classe _Program_ soit visible depuis le projet de tests, il faut ajouter une classe partielle à la fin du fichier _Program.cs_:

```cs
public partial class Program
{
}
```

## Initialisation de la WebApplicationFactory

Nous pouvons désormais initialiser notre WebApplicationFactory.

```cs
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using TechTalk.SpecFlow;

namespace MyApi.WebApi.Tests.Hooks;

[Binding]
internal class InitWebApplicationFactory
{
    internal const string HttpClientKey = nameof(HttpClientKey);
    internal const string ApplicationKey = nameof(ApplicationKey);

    [BeforeScenario]
    public void BeforeScenario(ScenarioContext scenarioContext)
    {
        var application = new WebApplicationFactory<Program>();

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
}

```

Nous allons maintenant ajouter un scénario de test afin de pouvoir accéder à notre API. Pour cela, ajouter un fichier specflow.

```
Features/WeatherWebApi.feature
```

avec le contenu suivant:

```
Feature: WeatherWebApi

Web API for weather forecasts

Scenario: Get weather forecasts
	When I make a GET request to 'weatherforecast'
	Then the response status code is '200'

```

Ensuite, ajouter un fichier permettant d'implémenter ces steps:

```
Features/WeatherWebApiSteps.cs
```

avec le contenu suivant:

```cs
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

```

Un repo contenant une solution est disponible ici:

```
git clone https://github.com/jtourvieille/DotNetIntegrationTests.git --branch feature/module2
```

[< précédent](../../Module%201%20création%20du%20projet%20de%20test/doc/Readme.md) | [suivant >](../../Module%203%20remplacement%20du%20système%20de%20log/doc/Readme.md)
