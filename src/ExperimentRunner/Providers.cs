using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ExperimentRunner;

public interface IModelProvider
{
    Task<GenerationResult> GenerateAsync(ModelConfig model, string prompt, CancellationToken stopToken);
}

public static class ModelProviderFactory
{
    public static IModelProvider Create(string provider) => provider.ToLowerInvariant() switch
    {
        "openai" => new OpenAiProvider(),
        "anthropic" => new AnthropicProvider(),
        "command" => new CommandProvider(),
        _ => throw new NotSupportedException($"Provider '{provider}' is not supported.")
    };
}

public sealed class OpenAiProvider : IModelProvider
{
    public async Task<GenerationResult> GenerateAsync(ModelConfig model, string prompt, CancellationToken stopToken)
    {
        var keyName = model.ApiKeyEnvironmentVariable ?? "OPENAI_API_KEY";
        var key = Environment.GetEnvironmentVariable(keyName)
            ?? throw new InvalidOperationException($"Environment variable {keyName} is not set.");
        var baseUrl = (model.BaseUrl ?? "https://api.openai.com").TrimEnd('/');
        using var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);

        var request = new Dictionary<string, object?>
        {
            ["model"] = model.Model,
            ["input"] = prompt,
            ["max_output_tokens"] = model.MaxOutputTokens
        };
        if (model.Temperature is not null) request["temperature"] = model.Temperature.Value;
        var body = JsonSerializer.Serialize(request);
        var sw = Stopwatch.StartNew();
        using var response = await client.PostAsync("/v1/responses", new StringContent(body, Encoding.UTF8, "application/json"), stopToken);
        var json = await response.Content.ReadAsStringAsync(stopToken);
        response.EnsureSuccessStatusCode();
        sw.Stop();

        using var doc = JsonDocument.Parse(json);
        var text = ExtractOpenAiText(doc.RootElement);
        int? input = null, output = null;
        if (doc.RootElement.TryGetProperty("usage", out var usage))
        {
            if (usage.TryGetProperty("input_tokens", out var i)) input = i.GetInt32();
            if (usage.TryGetProperty("output_tokens", out var o)) output = o.GetInt32();
        }
        var requestId = doc.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
        return new(text, json, requestId, sw.ElapsedMilliseconds, input, output);
    }

    private static string ExtractOpenAiText(JsonElement root)
    {
        var sb = new StringBuilder();
        if (!root.TryGetProperty("output", out var output)) return "";
        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content)) continue;
            foreach (var part in content.EnumerateArray())
                if (part.TryGetProperty("type", out var type) && type.GetString() == "output_text" && part.TryGetProperty("text", out var text))
                    sb.AppendLine(text.GetString());
        }
        return sb.ToString();
    }
}

public sealed class AnthropicProvider : IModelProvider
{
    public async Task<GenerationResult> GenerateAsync(ModelConfig model, string prompt, CancellationToken stopToken)
    {
        var keyName = model.ApiKeyEnvironmentVariable ?? "ANTHROPIC_API_KEY";
        var key = Environment.GetEnvironmentVariable(keyName)
            ?? throw new InvalidOperationException($"Environment variable {keyName} is not set.");
        var baseUrl = (model.BaseUrl ?? "https://api.anthropic.com").TrimEnd('/');
        using var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        client.DefaultRequestHeaders.Add("x-api-key", key);
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        var request = new Dictionary<string, object?>
        {
            ["model"] = model.Model,
            ["max_tokens"] = model.MaxOutputTokens,
            ["messages"] = new[] { new { role = "user", content = prompt } }
        };
        if (model.Temperature is not null) request["temperature"] = model.Temperature.Value;
        var body = JsonSerializer.Serialize(request);
        var sw = Stopwatch.StartNew();
        using var response = await client.PostAsync("/v1/messages", new StringContent(body, Encoding.UTF8, "application/json"), stopToken);
        var json = await response.Content.ReadAsStringAsync(stopToken);
        response.EnsureSuccessStatusCode();
        sw.Stop();

        using var doc = JsonDocument.Parse(json);
        var sb = new StringBuilder();
        foreach (var part in doc.RootElement.GetProperty("content").EnumerateArray())
            if (part.TryGetProperty("type", out var type) && type.GetString() == "text") sb.AppendLine(part.GetProperty("text").GetString());
        int? input = null, output = null;
        if (doc.RootElement.TryGetProperty("usage", out var usage))
        {
            if (usage.TryGetProperty("input_tokens", out var i)) input = i.GetInt32();
            if (usage.TryGetProperty("output_tokens", out var o)) output = o.GetInt32();
        }
        var requestId = doc.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
        return new(sb.ToString(), json, requestId, sw.ElapsedMilliseconds, input, output);
    }
}

public sealed class CommandProvider : IModelProvider
{
    public async Task<GenerationResult> GenerateAsync(ModelConfig model, string prompt, CancellationToken stopToken)
    {
        if (string.IsNullOrWhiteSpace(model.Command)) throw new InvalidOperationException("Command provider requires 'command'.");
        var psi = new ProcessStartInfo(model.Command, model.Arguments ?? "")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        var sw = Stopwatch.StartNew();
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Could not start model command.");
        await process.StandardInput.WriteAsync(prompt.AsMemory(), stopToken);
        process.StandardInput.Close();
        var stdoutTask = process.StandardOutput.ReadToEndAsync(stopToken);
        var stderrTask = process.StandardError.ReadToEndAsync(stopToken);
        await process.WaitForExitAsync(stopToken);
        sw.Stop();
        var stderr = await stderrTask;
        if (process.ExitCode != 0) throw new InvalidOperationException($"Model command failed: {stderr}");
        var text = await stdoutTask;
        return new(text, text, null, sw.ElapsedMilliseconds, null, null);
    }
}
