using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using Microsoft.AspNetCore.SignalR;
using WatchParty.Api.Extensions;
using WatchParty.Api.Hubs;
using WatchParty.Application.Features.Rooms;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Features.Videos;

namespace WatchParty.Api.Endpoints;

public static class VideoEndpoints
{
    public sealed record UpdateVideoRequest(string Title, string? Description);

    private const int MaxFormFieldLength = 8 * 1024;

    public static IEndpointRouteBuilder MapVideoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/videos").WithTags("Videos").RequireAuthorization();

        group.MapGet("/", async (string? search, string? filter, int? take, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
        {
            var libraryFilter = Enum.TryParse<LibraryFilter>(filter, ignoreCase: true, out var f) ? f : LibraryFilter.All;
            return Results.Ok(await sender.Send(new GetLibraryQuery(user.GetUserId(), search, libraryFilter, take), ct));
        });

        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetVideoQuery(id, user.GetUserId()), ct)).ToHttp());

        group.MapPost("/", UploadAsync);

        group.MapPut("/{id:guid}", async (Guid id, UpdateVideoRequest request, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
            (await sender.Send(new UpdateVideoCommand(id, user.GetUserId(), request.Title, request.Description), ct)).ToHttp());

        group.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, ISender sender,
            IHubContext<RoomHub, IRoomClient> hub, CancellationToken ct) =>
        {
            var result = await sender.Send(new DeleteVideoCommand(id, user.GetUserId()), ct);
            if (result.IsFailure) return result.Error.ToProblem();

            // Rooms that were playing it would otherwise be stuck with a broken player.
            foreach (var code in result.Value)
            {
                if ((await sender.Send(new GetPlaybackQuery(code), ct)) is { IsSuccess: true } state)
                    await hub.Clients.Group(RoomHub.GroupName(code)).VideoRemoved(new VideoRemovedEvent(id, state.Value));
            }
            return Results.NoContent();
        });

        group.MapPost("/{id:guid}/favorite", async (Guid id, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
            (await sender.Send(new ToggleFavoriteCommand(id, user.GetUserId()), ct))
                .ToHttp(isFavorite => Results.Ok(new { isFavorite })));

        // Range requests let the player seek anywhere without downloading the whole movie.
        group.MapGet("/{id:guid}/stream", async (Guid id, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetVideoMediaQuery(id, MediaKind.Video), ct))
                .ToHttp(file => Results.File(file.PhysicalPath, file.ContentType, enableRangeProcessing: true)));

        // Adaptive streaming: master.m3u8 and the files it references (relative paths).
        group.MapGet("/{id:guid}/hls/{**path}", async (Guid id, string path, ISender sender, HttpContext http, CancellationToken ct) =>
            (await sender.Send(new GetHlsFileQuery(id, path), ct)).ToHttp(file =>
            {
                http.Response.Headers.CacheControl = path.EndsWith(".ts", StringComparison.Ordinal) ? "private, max-age=86400" : "no-cache";
                return Results.File(file.PhysicalPath, file.ContentType, enableRangeProcessing: true);
            }));

        group.MapPost("/{id:guid}/subtitles", UploadSubtitleAsync).DisableAntiforgery();

        group.MapGet("/{id:guid}/subtitles/{subtitleId:guid}", async (Guid id, Guid subtitleId, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetSubtitleFileQuery(id, subtitleId), ct))
                .ToHttp(file => Results.File(file.PhysicalPath, file.ContentType)));

        group.MapDelete("/{id:guid}/subtitles/{subtitleId:guid}", async (Guid id, Guid subtitleId, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
            (await sender.Send(new DeleteSubtitleCommand(id, subtitleId, user.GetUserId()), ct)).ToHttp());

        group.MapGet("/{id:guid}/thumbnail", async (Guid id, ISender sender, HttpContext http, CancellationToken ct) =>
            (await sender.Send(new GetVideoMediaQuery(id, MediaKind.Thumbnail), ct)).ToHttp(file =>
            {
                http.Response.Headers.CacheControl = "private, max-age=86400";
                return Results.File(file.PhysicalPath, file.ContentType);
            }));

        return app;
    }

    /// <summary>
    /// Streams the multipart body straight to storage instead of buffering it, so multi-GB movies are fine.
    /// Text fields ("title", "description") must come before the file part.
    /// </summary>
    private static async Task<IResult> UploadAsync(HttpContext http, ClaimsPrincipal user, ISender sender, CancellationToken ct)
    {
        if (http.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } sizeLimit)
            sizeLimit.MaxRequestBodySize = null;

        if (!MediaTypeHeaderValue.TryParse(http.Request.ContentType, out var mediaType)
            || !mediaType.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase)
            || HeaderUtilities.RemoveQuotes(mediaType.Boundary).Value is not { Length: > 0 } boundary)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Envie o vídeo como multipart/form-data.");
        }

        var reader = new MultipartReader(boundary, http.Request.Body);
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        while (await reader.ReadNextSectionAsync(ct) is { } section)
        {
            if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition)) continue;

            if (disposition.IsFileDisposition())
            {
                var fileName = Path.GetFileName(HeaderUtilities.RemoveQuotes(
                    disposition.FileNameStar.HasValue ? disposition.FileNameStar : disposition.FileName).Value ?? "video");

                var title = fields.GetValueOrDefault("title");
                var result = await sender.Send(new UploadVideoCommand(
                    user.GetUserId(),
                    string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(fileName) : title,
                    fields.GetValueOrDefault("description"),
                    fileName,
                    section.Body,
                    http.Request.ContentLength), ct);

                return result.ToHttp(id => Results.Created($"/api/videos/{id}", new { id }));
            }

            if (disposition.IsFormDisposition() && HeaderUtilities.RemoveQuotes(disposition.Name).Value is { } name)
            {
                var buffer = new char[MaxFormFieldLength];
                using var textReader = new StreamReader(section.Body);
                var length = await textReader.ReadBlockAsync(buffer, ct);
                fields[name] = new string(buffer, 0, length);
            }
        }

        return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Nenhum arquivo foi enviado.");
    }

    /// <summary>Small file (≤ 2 MB): fields "label", "language" (optional) and "file" (.srt or .vtt).</summary>
    private static async Task<IResult> UploadSubtitleAsync(Guid id, HttpRequest request, ClaimsPrincipal user, ISender sender, CancellationToken ct)
    {
        if (!request.HasFormContentType) return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Envie a legenda como formulário.");

        var form = await request.ReadFormAsync(ct);
        var file = form.Files.GetFile("file");
        if (file is null) return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Nenhum arquivo foi enviado.");
        if (file.Length > 2 * 1024 * 1024) return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "A legenda deve ter até 2 MB.");

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);

        var label = form["label"].ToString();
        var result = await sender.Send(new UploadSubtitleCommand(
            id,
            user.GetUserId(),
            string.IsNullOrWhiteSpace(label) ? Path.GetFileNameWithoutExtension(file.FileName) : label,
            form["language"].ToString(),
            file.FileName,
            buffer.ToArray()), ct);

        return result.ToHttp(subtitle => Results.Created($"/api/videos/{id}/subtitles/{subtitle.Id}", subtitle));
    }
}
