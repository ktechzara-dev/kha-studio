using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Scripting;
using OpenCvSharp;
using VisionStudio.Core.Models;
using VisionStudio.Core.Types;

namespace VisionStudio.Scripting.Host;

/// <summary>
/// Result of compiling and running a Roslyn C# script.
/// </summary>
public sealed record ScriptExecutionResult(
    bool Success,
    object? ReturnValue,
    IReadOnlyList<string> Errors,
    TimeSpan ExecutionTime);

/// <summary>
/// Sandboxed Roslyn C# scripting host with namespace allow-listing, compilation caching,
/// timeout watchdog, and safe API access.
/// </summary>
public sealed class ScriptHost
{
    private static readonly string[] BlockedNamespaces = new[]
    {
        "System.IO.File",
        "System.IO.Directory",
        "System.Diagnostics.Process",
        "System.Reflection.Emit",
        "System.Net.Sockets",
        "System.Runtime.InteropServices",
        "Microsoft.Win32.Registry"
    };

    private readonly ScriptOptions _scriptOptions;
    private readonly ConcurrentDictionary<string, ScriptRunner<object>> _runnerCache = new(StringComparer.Ordinal);

    public ScriptHost()
    {
        _scriptOptions = ScriptOptions.Default
            .WithImports(
                "System",
                "System.Math",
                "System.Collections.Generic",
                "System.Linq",
                "System.Text",
                "OpenCvSharp",
                "VisionStudio.Core.Types",
                "VisionStudio.Core.Models",
                "VisionStudio.Core.Tools",
                "VisionStudio.Scripting.Host")
            .WithReferences(
                typeof(object).Assembly,
                typeof(Enumerable).Assembly,
                typeof(Mat).Assembly,
                typeof(VisionTable).Assembly,
                typeof(ImageFrame).Assembly,
                typeof(IScriptContext).Assembly);
    }

    /// <summary>
    /// Validates code against the security sandbox blacklist.
    /// </summary>
    public IReadOnlyList<string> ValidateSandbox(string code)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(code)) return errors;

        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();

        foreach (var blocked in BlockedNamespaces)
        {
            if (code.Contains(blocked, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Sử dụng không hợp lệ: '{blocked}' bị cấm trong môi trường sandbox an toàn.");
            }
        }

        return errors;
    }

    /// <summary>
    /// Compiles a C# script and returns any compiler diagnostics.
    /// </summary>
    public (bool Success, IReadOnlyList<string> Diagnostics) Compile(string code)
    {
        var sandboxErrors = ValidateSandbox(code);
        if (sandboxErrors.Count > 0)
        {
            return (false, sandboxErrors);
        }

        try
        {
            // Rewrites loops (while, for, foreach, do) to inject cooperative timeout watchdog __CheckTimeout()
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();
            var rewriter = new LoopTimeoutRewriter();
            var rewrittenRoot = rewriter.Visit(root);
            var rewrittenCode = rewrittenRoot.ToFullString();

            var script = CSharpScript.Create(rewrittenCode, _scriptOptions, typeof(ScriptGlobals));
            var diagnostics = script.Compile();

            var errors = diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => $"Dòng {d.Location.GetLineSpan().StartLinePosition.Line + 1}: {d.GetMessage()}")
                .ToList();

            if (errors.Count > 0)
            {
                return (false, errors);
            }

            var runner = script.CreateDelegate();
            _runnerCache[code] = runner;
            return (true, Array.Empty<string>());
        }
        catch (Exception ex)
        {
            return (false, new[] { ex.Message });
        }
    }

    /// <summary>
    /// Executes a C# script within the safe sandbox, enforcing timeout and cancellation.
    /// </summary>
    public async Task<ScriptExecutionResult> ExecuteAsync(
        string code,
        IScriptContext context,
        int timeoutMs = 3000,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        // 1. Get or compile runner delegate
        if (!_runnerCache.TryGetValue(code, out var runner))
        {
            var (success, diagnostics) = Compile(code);
            if (!success)
            {
                return new ScriptExecutionResult(false, null, diagnostics, sw.Elapsed);
            }
            runner = _runnerCache[code];
        }

        // 2. Set up watchdog timeout cancellation
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        var globals = new ScriptGlobals(context)
        {
            CancellationToken = cts.Token
        };

        try
        {
            var runTask = Task.Run(async () => await runner(globals, cts.Token).ConfigureAwait(false), cts.Token);
            var completedTask = await Task.WhenAny(runTask, Task.Delay(timeoutMs)).ConfigureAwait(false);

            if (completedTask != runTask)
            {
                cts.Cancel();
                return new ScriptExecutionResult(
                    Success: false,
                    ReturnValue: null,
                    Errors: new[] { $"Thực thi script quá thời gian cho phép ({timeoutMs} ms)." },
                    ExecutionTime: sw.Elapsed);
            }

            var result = await runTask.ConfigureAwait(false);
            return new ScriptExecutionResult(true, result, Array.Empty<string>(), sw.Elapsed);
        }
        catch (OperationCanceledException)
        {
            return new ScriptExecutionResult(
                Success: false,
                ReturnValue: null,
                Errors: new[] { $"Thực thi script quá thời gian cho phép ({timeoutMs} ms)." },
                ExecutionTime: sw.Elapsed);
        }
        catch (TimeoutException)
        {
            return new ScriptExecutionResult(
                Success: false,
                ReturnValue: null,
                Errors: new[] { $"Thực thi script quá thời gian cho phép ({timeoutMs} ms)." },
                ExecutionTime: sw.Elapsed);
        }
        catch (Exception ex)
        {
            var inner = ex;
            while (inner.InnerException != null) inner = inner.InnerException;
            if (inner is TimeoutException or OperationCanceledException)
            {
                return new ScriptExecutionResult(
                    Success: false,
                    ReturnValue: null,
                    Errors: new[] { $"Thực thi script quá thời gian cho phép ({timeoutMs} ms)." },
                    ExecutionTime: sw.Elapsed);
            }

            return new ScriptExecutionResult(
                Success: false,
                ReturnValue: null,
                Errors: new[] { $"Lỗi thực thi script: {inner.Message}" },
                ExecutionTime: sw.Elapsed);
        }
    }

    private sealed class LoopTimeoutRewriter : CSharpSyntaxRewriter
    {
        private static readonly StatementSyntax CheckStmt = SyntaxFactory.ParseStatement("__CheckTimeout();\n");

        public override SyntaxNode? VisitWhileStatement(WhileStatementSyntax node)
        {
            var visited = (WhileStatementSyntax)base.VisitWhileStatement(node)!;
            return visited.WithStatement(Inject(visited.Statement));
        }

        public override SyntaxNode? VisitForStatement(ForStatementSyntax node)
        {
            var visited = (ForStatementSyntax)base.VisitForStatement(node)!;
            return visited.WithStatement(Inject(visited.Statement));
        }

        public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
        {
            var visited = (ForEachStatementSyntax)base.VisitForEachStatement(node)!;
            return visited.WithStatement(Inject(visited.Statement));
        }

        public override SyntaxNode? VisitDoStatement(DoStatementSyntax node)
        {
            var visited = (DoStatementSyntax)base.VisitDoStatement(node)!;
            return visited.WithStatement(Inject(visited.Statement));
        }

        private static StatementSyntax Inject(StatementSyntax statement)
        {
            if (statement is BlockSyntax block)
            {
                return block.WithStatements(block.Statements.Insert(0, CheckStmt));
            }
            return SyntaxFactory.Block(CheckStmt, statement);
        }
    }
}
