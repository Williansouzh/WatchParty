using System.Diagnostics;

namespace WatchParty.Infrastructure.Media;

internal static class ProcessRunner
{
    public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

    /// <param name="onOutputLine">When set, standard output is streamed line by line instead of collected.</param>
    public static async Task<ProcessResult> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken,
        Action<string>? onOutputLine = null)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdout = onOutputLine is null
            ? process.StandardOutput.ReadToEndAsync(CancellationToken.None)
            : StreamLinesAsync(process.StandardOutput, onOutputLine);
        var stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            throw;
        }

        return new ProcessResult(process.ExitCode, await stdout, await stderr);
    }

    private static async Task<string> StreamLinesAsync(StreamReader reader, Action<string> onLine)
    {
        while (await reader.ReadLineAsync() is { } line) onLine(line);
        return string.Empty;
    }
}
