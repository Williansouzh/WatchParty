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
    public sealed record CreateRoomRequest(string Name, Guid? VideoId, Guid? PlaylistId = null);

    public static IEndpointRouteBuilder MapRoomEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/rooms").WithTags("Rooms").RequireAuthorization();

        group.MapGet("/", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetRoomsQuery(), ct)));

        group.MapPost("/", async (CreateRoomRequest request, ClaimsPrincipal user, ISender sender,
            IHubContext<RoomHub, IRoomClient> hub, CancellationToken ct) =>
        {
            var result = await sender.Send(new CreateRoomCommand(user.GetUserId(), request.Name, request.VideoId, request.PlaylistId), ct);
            if (result.IsFailure) return result.Error.ToProblem();

            var room = await sender.Send(new GetRoomQuery(result.Value, user.GetUserId()), ct);
            if (room.IsSuccess)
            {
                await hub.Clients.Group(RoomHub.LobbyGroup).RoomActivity(new RoomActivityEvent(
                    "created", room.Value.Code, room.Value.Name, room.Value.CurrentVideo?.Title, user.GetUserId(), user.GetDisplayName()));
            }
            return Results.Created($"/api/rooms/{result.Value}", new { code = result.Value });
        });

        group.MapGet("/{code}", async (string code, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetRoomQuery(code, user.GetUserId()), ct)).ToHttp());

        group.MapDelete("/{code}", async (string code, ClaimsPrincipal user, ISender sender,
            IHubContext<RoomHub, IRoomClient> hub, CancellationToken ct) =>
        {
            var result = await sender.Send(new DeleteRoomCommand(code, user.GetUserId()), ct);
            if (result.IsSuccess) await hub.Clients.Group(RoomHub.GroupName(RoomCode.Normalize(code))).RoomDeleted();
            return result.ToHttp();
        });

        app.MapGet("/api/config", (IOptions<WebRtcOptions> webRtc, IOptions<VoiceOptions> voice) => Results.Ok(new
        {
            voice = voice.Value.UseLiveKit ? "livekit" : "mesh",
            liveKitUrl = voice.Value.UseLiveKit ? voice.Value.LiveKit.Url : null,
            iceServers = webRtc.Value.IceServers.Select(s => new { urls = s.Urls, username = s.Username, credential = s.Credential })
        })).RequireAuthorization();

        return app;
    }
}
