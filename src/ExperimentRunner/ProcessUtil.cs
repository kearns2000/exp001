using System.Diagnostics;

namespace ExperimentRunner;

public sealed record ProcessResult(int ExitCode, string StdOut, string StdErr, long DurationMs);

public static class ProcessUtil
{
    public static async Task<ProcessResult> RunAsync(string fileName, string arguments, string workingDirectory, TimeSpan timeout, CancellationToken stopToken)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        var sw = Stopwatch.StartNew();
        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Could not start {fileName}.");
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stopToken);
        timeoutCts.CancelAfter(timeout);
        var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw;
        }
        sw.Stop();
        return new(process.ExitCode, await stdoutTask, await stderrTask, sw.ElapsedMilliseconds);
    }
}
