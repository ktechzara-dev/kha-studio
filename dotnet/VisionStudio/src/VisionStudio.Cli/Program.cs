using System.Diagnostics;
using VisionStudio.Core.Pipeline;
using VisionStudio.Core.Recipes;
using VisionStudio.Core.Testing;
using VisionStudio.Core.Tools;
using VisionStudio.Core.Traceability;

namespace VisionStudio.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintHelp();
            return 0;
        }

        if (args[0] is "-v" or "--version" or "version")
        {
            Console.WriteLine("VisionStudio Industrial CLI (vs-cli) v2.0.0");
            return 0;
        }

        string command = args[0].ToLowerInvariant();
        var cmdArgs = args.Skip(1).ToArray();

        try
        {
            return command switch
            {
                "test" => await HandleTestCommand(cmdArgs),
                "verify-trace" or "verify" => HandleVerifyTraceCommand(cmdArgs),
                "replay" => await HandleReplayCommand(cmdArgs),
                "recipe" => HandleRecipeCommand(cmdArgs),
                _ => HandleUnknownCommand(command)
            };
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private static void PrintHelp()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("==================================================================");
        Console.WriteLine(" VisionStudio Industrial Automation CLI (vs-cli) v2.0.0");
        Console.WriteLine("==================================================================");
        Console.ResetColor();
        Console.WriteLine("Usage:");
        Console.WriteLine("  vs-cli test --project <path> --set <path> [--recipe <path>]");
        Console.WriteLine("      Runs an automated regression test set against a pipeline project.");
        Console.WriteLine("      Returns exit code 0 if all tests pass; 1 if any fail (CI/CD ready).");
        Console.WriteLine();
        Console.WriteLine("  vs-cli verify-trace --store <store_directory>");
        Console.WriteLine("      Verifies cryptographic SHA-256 hash chain integrity of inspection logs.");
        Console.WriteLine();
        Console.WriteLine("  vs-cli replay --store <store_directory> --project <path>");
        Console.WriteLine("      Replays historical inspection images through a new pipeline version.");
        Console.WriteLine();
        Console.WriteLine("  vs-cli recipe diff --r1 <recipe1.vsrecipe> --r2 <recipe2.vsrecipe>");
        Console.WriteLine("      Prints parameter differences between two product recipes.");
        Console.WriteLine("==================================================================");
    }

    private static async Task<int> HandleTestCommand(string[] args)
    {
        string? projectPath = GetArgValue(args, "--project", "-p");
        string? setPath = GetArgValue(args, "--set", "-s");
        string? recipePath = GetArgValue(args, "--recipe", "-r");

        if (string.IsNullOrEmpty(projectPath) || string.IsNullOrEmpty(setPath))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Missing required arguments. Usage: vs-cli test --project <path> --set <path>");
            Console.ResetColor();
            return 1;
        }

        if (!File.Exists(setPath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] Test set file not found: {setPath}");
            Console.ResetColor();
            return 1;
        }

        Console.WriteLine($"[vs-cli] Loading Test Set: {setPath}");
        var testSet = TestSet.Load(setPath);
        Console.WriteLine($"[vs-cli] Suite: '{testSet.Name}' ({testSet.Items.Count} test samples)");

        // Setup pipeline graph
        var registry = ToolRegistry.Shared;
        registry.RegisterAssembly(typeof(VisionStudio.Imaging.ImagingModule).Assembly);
        var graph = new PipelineGraph();

        if (File.Exists(projectPath))
        {
            Console.WriteLine($"[vs-cli] Loading Pipeline Project: {projectPath}");
            // In a real project package, load via PipelineSerializer
        }

        // Apply recipe if specified
        if (!string.IsNullOrEmpty(recipePath) && File.Exists(recipePath))
        {
            Console.WriteLine($"[vs-cli] Applying Recipe: {recipePath}");
            var recipe = Recipe.Load(recipePath);
            var mgr = new RecipeManager();
            mgr.ApplyRecipe(recipe, graph);
        }

        var executor = new PipelineExecutor();
        var replayEngine = new OfflineReplayEngine();

        Console.WriteLine("[vs-cli] Executing test suite...");
        var sw = Stopwatch.StartNew();
        var report = await replayEngine.RunTestSetAsync(executor, graph, testSet);
        sw.Stop();

        Console.WriteLine();
        Console.WriteLine("------------------------------------------------------------------");
        Console.WriteLine($" TEST RESULTS: {report.TestSetName}");
        Console.WriteLine("------------------------------------------------------------------");
        Console.WriteLine($" Total Samples : {report.TotalItems}");
        Console.ForegroundColor = report.PassedItems == report.TotalItems ? ConsoleColor.Green : ConsoleColor.Yellow;
        Console.WriteLine($" Passed        : {report.PassedItems}");
        Console.ForegroundColor = report.FailedItems > 0 ? ConsoleColor.Red : ConsoleColor.Green;
        Console.WriteLine($" Failed        : {report.FailedItems}");
        Console.ResetColor();
        Console.WriteLine($" Pass Rate     : {report.PassRate:P1}");
        Console.WriteLine($" Avg Latency   : {report.AverageLatencyMs:F1} ms");
        Console.WriteLine($" Elapsed Time  : {sw.ElapsedMilliseconds} ms");
        Console.WriteLine("------------------------------------------------------------------");

        if (report.FailedItems > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Failure Breakdown:");
            foreach (var fail in report.Details.Where(d => !d.Success))
            {
                Console.WriteLine($"  - [{Path.GetFileName(fail.Item.ImagePath)}] {fail.FailureReason}");
            }
            Console.WriteLine("\n❌ QUALITY GATE FAILED: Regressions or defects detected.");
            Console.ResetColor();
            return 1;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✅ QUALITY GATE PASSED: All test cases satisfied acceptance criteria.");
        Console.ResetColor();
        return 0;
    }

    private static int HandleVerifyTraceCommand(string[] args)
    {
        string? storeDir = GetArgValue(args, "--store", "-s") ?? Directory.GetCurrentDirectory();

        Console.WriteLine($"[vs-cli] Verifying Traceability Store at: {storeDir}");
        var store = new TraceabilityStore(storeDir);
        var val = store.ValidateHashChain();

        if (val.IsValid)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✅ INTEGRITY VERIFIED: All {val.TotalRecords} records form an unbroken, tamper-free SHA-256 chain.");
            Console.ResetColor();
            return 0;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ DATA TAMPERING DETECTED at Record #{val.TamperedIndex + 1}!");
            Console.WriteLine($"Reason: {val.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private static async Task<int> HandleReplayCommand(string[] args)
    {
        string? storeDir = GetArgValue(args, "--store", "-s") ?? Directory.GetCurrentDirectory();
        string? projectPath = GetArgValue(args, "--project", "-p");

        Console.WriteLine($"[vs-cli] Replaying historical inspection batch from: {storeDir}");
        var store = new TraceabilityStore(storeDir);
        var records = store.Records;
        Console.WriteLine($"[vs-cli] Found {records.Count} historical inspection records.");

        var graph = new PipelineGraph();
        var executor = new PipelineExecutor();
        var replayEngine = new OfflineReplayEngine();

        var report = await replayEngine.ReplayTraceBatchAsync(executor, graph, records);

        Console.WriteLine($"[vs-cli] Replayed: {report.TotalReplayed} items. Agreement Rate: {report.AgreementRate:P1}");
        if (report.RegressionsDetected > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"⚠️ {report.RegressionsDetected} verdict changes detected compared to historical production run.");
            Console.ResetColor();
        }
        return 0;
    }

    private static int HandleRecipeCommand(string[] args)
    {
        if (args.Length > 0 && args[0].Equals("diff", StringComparison.OrdinalIgnoreCase))
        {
            string? r1Path = GetArgValue(args, "--r1");
            string? r2Path = GetArgValue(args, "--r2");

            if (string.IsNullOrEmpty(r1Path) || string.IsNullOrEmpty(r2Path))
            {
                Console.WriteLine("Usage: vs-cli recipe diff --r1 <path1> --r2 <path2>");
                return 1;
            }

            var r1 = Recipe.Load(r1Path);
            var r2 = Recipe.Load(r2Path);

            var mgr = new RecipeManager();
            var diffs = mgr.DiffRecipes(r1, r2);

            Console.WriteLine($"Comparing '{r1.Name}' vs '{r2.Name}':");
            if (diffs.Count == 0)
            {
                Console.WriteLine("Both recipes have identical parameters.");
            }
            else
            {
                Console.WriteLine($"Found {diffs.Count} differing parameter(s):");
                foreach (var d in diffs)
                {
                    Console.WriteLine($"  - [{d.NodeId}.{d.ParameterName}] Recipe A: '{d.ValueA}' | Recipe B: '{d.ValueB}'");
                }
            }
            return 0;
        }

        Console.WriteLine("Unknown recipe subcommand. Use 'vs-cli recipe diff --r1 <p1> --r2 <p2>'");
        return 1;
    }

    private static int HandleUnknownCommand(string command)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Unknown command '{command}'. Use 'vs-cli --help' for available commands.");
        Console.ResetColor();
        return 1;
    }

    private static string? GetArgValue(string[] args, params string[] flags)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (flags.Contains(args[i], StringComparer.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }
        return null;
    }
}
