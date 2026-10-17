using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using WatchParty.Api.Common;
using WatchParty.Api.Extensions;
using WatchParty.Api.Hubs;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Features.Rooms;
using WatchParty.Domain.Rooms;

namespace WatchParty.Api.Endpoints;

public static class RoomEndpoints
{
    public sealed record CreateRoomRequest(string Name, Guid? VideoId);

    public static IEndpointRouteBuilder MapRoomEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/rooms").WithTags("Rooms").RequireAuthorization();

        group.MapGet("/", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetRoomsQuery(), ct)));

        group.MapPost("/", async (CreateRoomRequest request, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
            (await sender.Send(new CreateRoomCommand(user.GetUserId(), request.Name, request.VideoId), ct))
                .ToHttp(code => Results.Created($"/api/rooms/{code}", new { code })));

        group.MapGet("/{code}", async (string code, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetRoomQuery(code, user.GetUserId()), ct)).ToHttp());

        group.MapDelete("/{code}", async (string code, ClaimsPrincipal user, ISender sender,
            IHubContext<RoomHub, IRoomClient> hub, CancellationToken ct) =>
        {
            var result = await sender.Send(new DeleteRoomCommand(code, user.GetUserId()), ct);
            if (result.IsSuccess) await hub.Clients.Group(RoomHub.GroupName(RoomCode.Normalize(code))).RoomDeleted();
            return result.ToHttp();
        });

        app.MapGet("/api/config", (IOptions<WebRtcOptions> webRtc) => Results.Ok(new
        {
            iceServers = webRtc.Value.IceServers.Select(s => new { urls = s.Urls, username = s.Username, credential = s.Credential })
        })).RequireAuthorization();

        return app;
    }
}
