# Module 3: remplacement du système de log

Démarrer avec le projet créé au [module 2](https://github.com/jtourvieille/DotNetIntegrationTests/tree/main/modules/Module%202%20lancement%20des%20appels%20http/src/MyApi)

## Ajout d'un système de log

Pour commencer, ajoutons un système de log. Dans le fichier _Program.cs_, ajouter la ligne

```cs
builder.Logging.AddConsole();
```

On choisit ici arbitrairement le log console pour des raisons de simplicité.

## Suppression des références au système de log existant

Puisque l'initialisation de l'application est faite via 

```cs
WebApplicationFactory<Program>()
```

C'est bien les enregistrement de loggers faits dans l'implémentation de l'application elle-même qui sont utilisés dans les tests.

Ce que nous souhaitons, c'est pouvoir remplacer ce système de logging choisi pour nos tests.

Nous allons d'abord commencer par le supprimer, en ajoutant les lignes suivantes dans le fichier _InitWebApplicationFactory_:

```cs
var application = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
{
    builder.ConfigureTestServices(services =>
    {
        RemoveLogging(services);
    });
});
```

On va venir ici configurer des services spécifiquement pour les tests.

La méthode RemoveLogging:

```cs
private static void RemoveLogging(IServiceCollection services)
{
    services.RemoveAll(typeof(ILogger<>));
    services.RemoveAll<ILogger>();
}
```

## Ajout du système de log NullLogger

Enfin, nous pouvons ajouter notre propre service de logging.

Ici, nous allons utiliser le _NullLogger_ fourni par Microsoft, qui permet de ne logguer nul part. Pour ce faire, il suffit de modifier légèrement l'implémentation précédente:

```cs
var application = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
{
    builder.ConfigureTestServices(services =>
    {
        ReplaceLogging(services);
    });
});
```
```cs
private static void ReplaceLogging(IServiceCollection services)
{
    services.RemoveAll(typeof(ILogger<>));
    services.RemoveAll<ILogger>();
    services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
}
```

Un repo contenant une solution est disponible [ici](https://github.com/jtourvieille/DotNetIntegrationTests/tree/main/modules/Module%203%20remplacement%20du%20système%20de%20log/src/MyApi)

[< précédent](../../Module%202%20lancement%20des%20appels%20http/doc/Readme.md) | [suivant >](../../Module%204%20remplacement%20de%20la%20database/doc/Readme.md)
