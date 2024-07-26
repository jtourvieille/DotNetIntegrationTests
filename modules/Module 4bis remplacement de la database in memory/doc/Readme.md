# Module 4bis: remplacement de la database in memory

Le module 4 vient avec certaines contraintes: docker doit être installé et fonctionnel sur les environnements de développement et sur les CI, ce qui n'est pas toujours possible.

Ici, nous allons utiliser une version "dégradée", puisqu'elle fait appel à une database en mémoire. Cette solution a aussi des inconvénients, qui sont listés [ici](https://learn.microsoft.com/en-us/ef/core/testing/). Parmi les principales, on peut citer l'absence de transaction, la sensibilité à la casse, etc. Il s'agit d'un moyen non conseillé, par qui peut permettre sur des cas simples d'avoir une solution. Une autre alternative serait d'utiliser SQLite, mais même si elle en comporte moins, cette solution est également sujette à des limitations, listées sur la page ci-dessus.

On repart des sources [ici](https://github.com/jtourvieille/DotNetIntegrationTests/tree/main/modules/Module%203%20remplacement%20du%20syst%C3%A8me%20de%20log/src/MyApi) pour la solution. On déroule les étapes de création de la database du [module 4](https://github.com/jtourvieille/DotNetIntegrationTests/blob/main/modules/Module%204%20remplacement%20de%20la%20database/doc/Readme.md), ainsi que son utilisation dans l'implémentation.

Dans le projet de test, ajouter une référence à

```
Microsoft.EntityFrameworkCore.InMemory
```

Puis créer la méthode _ReplaceDatabase_ dans _InitWebApplicationFactory_:

```cs
private static void ReplaceDatabase(IServiceCollection services)
{
    services.RemoveAll<DbContextOptions<WeatherContext>>();
    services.RemoveAll<WeatherContext>();

    services.AddDbContext<WeatherContext>(options =>
        options.UseInMemoryDatabase("TestingWeatherContext"));
}
```

Puis on l'appelle dans _ConfigureTestServices_, après le remplacement du logging:

```cs
builder.ConfigureTestServices(services =>
{
    ReplaceLogging(services);
    ReplaceDatabase(services);
});
```

Ensuite, il nous faut ajouter la base de données au DI de Specflow, pour qu'il puisse la récupérer. Pour cela, on va ajouter une classe _InMemoryDatabase_ qui contiendra des méthodes taggués BeforeScenario & AfterScenario:

```cs
using BoDi;
using Microsoft.EntityFrameworkCore;
using TechTalk.SpecFlow;

namespace MyApi.WebApi.Tests.Hooks;

[Binding]
internal sealed class InMemoryDatabase
{
    [BeforeScenario]
    public static void BeforeScenario(IObjectContainer objectContainer)
    {
        var scchContextOptions = new DbContextOptionsBuilder<WeatherContext>()
            .UseInMemoryDatabase("TestingWeatherContext")
            .Options;

        var database = new WeatherContext(scchContextOptions);
        database.Database.EnsureDeleted();
        objectContainer.RegisterInstanceAs(database);
    }

    [AfterScenario]
    public static void AfterScenario(IObjectContainer objectContainer)
    {
        WeatherContext? database = objectContainer.Resolve<WeatherContext>();

        if (database is not null)
        {
            database.Database.EnsureDeleted();
        }
    }
}

```

Evidemment, il est toujours possible d'initialiser la database avec des données via un script SQL.

Par contre, utiliser Respawner ici n'apporte pas grand chose, puisque la base de données qu'on a initialisée est recréée à chaque scenario. En revanche, ce qui peut être intéressant de mettre en place, est le jeu en séquentiel des tests. En effet, un test peut avoir un comportement non idempotent vis-à-vis de la base de données. Il ne faudrait pas que cela vienne perturber d'autres tests. Pour cela, il faut simplement ajouter cet attribut dans le projet de tests:

```cs
[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly)]

```

[< précédent](../../Module%204%20remplacement%20de%20la%20database/doc/Readme.md) | [suivant >](../../Module%205%20ajout%20de%20tests/doc/Readme.md)
