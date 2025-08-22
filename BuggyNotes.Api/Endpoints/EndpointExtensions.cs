using System.Reflection;
using Microsoft.AspNetCore.Routing;

namespace BuggyNotes.Api.Endpoints;

public static class EndpointExtensions
{
    public static void MapEndpointsFromAssemblyContaining<T>(this IEndpointRouteBuilder app)
        => app.MapEndpointsFromAssembly(typeof(T).Assembly);

    public static void MapEndpointsFromAssembly(this IEndpointRouteBuilder app, Assembly assembly)
    {
        var endpointTypes = assembly.DefinedTypes
            .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IEndpoint).IsAssignableFrom(t));

        foreach (var type in endpointTypes)
        {
            var m = type.GetMethod(nameof(IEndpoint.MapEndpoint));
            m!.Invoke(null, new object[] { app });
        }
    }
}

