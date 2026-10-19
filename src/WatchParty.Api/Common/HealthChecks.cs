using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using WatchParty.Application;
using WatchParty.Application.Abstractions.Services;

namespace WatchParty.Api.Common;

/// <summary>Unhealthy when the disk can't take more uploads.</summary>
internal sealed class DiskSpaceHealthCheck(IVideoStorage storage, IOptions<StorageLimitsOptions> limits) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var free = storage.GetAvailableFreeBytes();
        if (free is null) return Task.FromResult(HealthCheckResult.Healthy("Espaço livre desconhecido."));

        var data = new Dictionary<string, object> { ["freeGb"] = Math.Round(free.Value / 1024d / 1024 / 1024, 1) };
        var minimum = limits.Value.MinFreeBytes;
        return Task.FromResult(free < minimum
            ? HealthCheckResult.Unhealthy("Pouco espaço em disco.", data: data)
            : free < minimum * 3
                ? HealthCheckResult.Degraded("Espaço em disco acabando.", data: data)
                : HealthCheckResult.Healthy(data: data));
    }
}

internal static class HealthResponse
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(e => e.Key, e => new
            {
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                data = e.Value.Data
            })
        }));
    }
}
