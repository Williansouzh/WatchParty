using System.Security.Claims;
using WatchParty.Api.Extensions;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Features.Playlists;

namespace WatchParty.Api.Endpoints;

public static class PlaylistEndpoints
{
    public sealed record PlaylistRequest(string Name, string? Description);
    public sealed record AddVideoRequest(Guid VideoId);

    public static IEndpointRouteBuilder MapPlaylistEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/playlists").WithTags("Playlists").RequireAuthorization();

        group.MapGet("/", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetPlaylistsQuery(), ct)));

        group.MapPost("/", async (PlaylistRequest request, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
            (await sender.Send(new CreatePlaylistCommand(user.GetUserId(), request.Name, request.Description), ct))
                .ToHttp(id => Results.Created($"/api/playlists/{id}", new { id })));

        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetPlaylistQuery(id, user.GetUserId()), ct)).ToHttp());

        group.MapPut("/{id:guid}", async (Guid id, PlaylistRequest request, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
            (await sender.Send(new UpdatePlaylistCommand(id, user.GetUserId(), request.Name, request.Description), ct)).ToHttp());

        group.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
            (await sender.Send(new DeletePlaylistCommand(id, user.GetUserId()), ct)).ToHttp());

        group.MapPost("/{id:guid}/videos", async (Guid id, AddVideoRequest request, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
            (await sender.Send(new AddVideoToPlaylistCommand(id, request.VideoId, user.GetUserId()), ct)).ToHttp());

        group.MapDelete("/{id:guid}/videos/{videoId:guid}", async (Guid id, Guid videoId, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
            (await sender.Send(new RemoveVideoFromPlaylistCommand(id, videoId, user.GetUserId()), ct)).ToHttp());

        return app;
    }
}
