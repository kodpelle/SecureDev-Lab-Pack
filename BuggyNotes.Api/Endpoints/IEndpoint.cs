using Microsoft.AspNetCore.Routing;

namespace BuggyNotes.Api.Endpoints;

public interface IEndpoint
{
    static abstract void MapEndpoint(IEndpointRouteBuilder app);
}
