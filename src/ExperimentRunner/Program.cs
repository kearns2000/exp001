using System.Text.Json;

namespace ExperimentRunner;

public static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintHelp();
            return 2;
        }

        try
        {
            return args[0].ToLowerInvariant() switch
            {
                "plan" => await PlanAsync(args),
                "run" => await RunAsync(args),
                "aggregate" => await AggregateAsync(args),
                "blind-review" => await BlindReviewAsync(args),
                _ => throw new ArgumentException($"Unknown command '{args[0]}'.")
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static async Task<int> PlanAsync(string[] args)
    {
        var config = await LoadConfigAsync(RequiredArg(args, 1, "config path"));
        var root = ResolveRepositoryRoot();
        var tasks = ExperimentRunnerService.LoadTasks(Path.Combine(root, config.BenchmarkRoot), JsonOptions);
        var plan = ExperimentRunnerService.BuildPlan(config, tasks);
        Console.WriteLine($"Experiment: {config.ExperimentId}");
        Console.WriteLine($"Tasks: {tasks.Count}");
        Console.WriteLine($"Models: {config.Models.Count}");
        Console.WriteLine($"Candidates: {plan.Count}");
        foreach (var model in config.Models)
            Console.WriteLine($"  {model.Id}: {tasks.Count * model.Repetitions}");
        return 0;
    }

    private static async Task<int> RunAsync(string[] args)
    {
        var configPath = RequiredArg(args, 1, "config path");
        var config = await LoadConfigAsync(configPath);
        var root = ResolveRepositoryRoot();
        var runner = new ExperimentRunnerService(root, config, JsonOptions);
        await runner.RunAsync();
        return 0;
    }

    private static async Task<int> AggregateAsync(string[] args)
    {
        var config = await LoadConfigAsync(RequiredArg(args, 1, "config path"));
        var root = ResolveRepositoryRoot();
        var resultsDir = Path.Combine(root, config.ResultsRoot, config.ExperimentId);
        await Aggregator.WriteOutputsAsync(resultsDir, JsonOptions);
        return 0;
    }

    private static async Task<int> BlindReviewAsync(string[] args)
    {
        var config = await LoadConfigAsync(RequiredArg(args, 1, "config path"));
        var root = ResolveRepositoryRoot();
        var resultsDir = Path.Combine(root, config.ResultsRoot, config.ExperimentId);
        await Aggregator.WriteBlindReviewPackAsync(resultsDir, JsonOptions);
        return 0;
    }

    private static async Task<ExperimentConfig> LoadConfigAsync(string path)
    {
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<ExperimentConfig>(json, JsonOptions)
            ?? throw new InvalidOperationException("Could not deserialize experiment config.");
    }

    private static string RequiredArg(string[] args, int index, string name) =>
        args.Length > index ? Path.GetFullPath(args[index]) : throw new ArgumentException($"Missing {name}.");

    private static string ResolveRepositoryRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "EMSE.SecurityExperiment.sln")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Run from within the experiment repository.");
    }

    private static void PrintHelp()
    {
        Console.WriteLine("EMSE Security Experiment");
        Console.WriteLine("  dotnet run --project src/ExperimentRunner -- plan config/experiment.json");
        Console.WriteLine("  dotnet run --project src/ExperimentRunner -- run config/experiment.json");
        Console.WriteLine("  dotnet run --project src/ExperimentRunner -- aggregate config/experiment.json");
        Console.WriteLine("  dotnet run --project src/ExperimentRunner -- blind-review config/experiment.json");
    }
}
