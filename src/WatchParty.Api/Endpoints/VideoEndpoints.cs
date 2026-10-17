using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using WatchParty.Api.Extensions;
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

        group.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
            (await sender.Send(new DeleteVideoCommand(id, user.GetUserId()), ct)).ToHttp());

        group.MapPost("/{id:guid}/favorite", async (Guid id, ClaimsPrincipal user, ISender sender, CancellationToken ct) =>
            (await sender.Send(new ToggleFavoriteCommand(id, user.GetUserId()), ct))
                .ToHttp(isFavorite => Results.Ok(new { isFavorite })));

        // Range requests let the player seek anywhere without downloading the whole movie.
        group.MapGet("/{id:guid}/stream", async (Guid id, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetVideoMediaQuery(id, MediaKind.Video), ct))
                .ToHttp(file => Results.File(file.PhysicalPath, file.ContentType, enableRangeProcessing: true)));

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
                    section.Body), ct);

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
}
