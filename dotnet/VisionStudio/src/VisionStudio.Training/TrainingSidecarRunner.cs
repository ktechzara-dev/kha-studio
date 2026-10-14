using System.Diagnostics;
using System.Text.Json;
using VisionStudio.DeepLearning.Datasets;
using VisionStudio.DeepLearning.Evaluation;
using VisionStudio.DeepLearning.Model;
using ModelMeta = VisionStudio.DeepLearning.Model.ModelMetadata;

namespace VisionStudio.Training;

public sealed record TrainingResult(
    bool Success,
    string ModelPath,
    ModelMeta? Metadata,
    object? Metrics,
    string? ErrorMessage = null);

public sealed class TrainingSidecarRunner
{
    private readonly PureEngineTrainer _pureEngineTrainer = new();

    /// <summary>
    /// Executes model training using either pure deterministic C# engine or an external Python sidecar process.
    /// </summary>
    public async Task<TrainingResult> RunTrainingAsync(
        TrainingConfig config,
        DatasetManager dataset,
        string? pythonScriptPath = null,
        string? pythonExecutable = null,
        IProgress<TrainingProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (dataset.Items.Count == 0)
        {
            return new TrainingResult(false, string.Empty, null, null, "Dataset is empty. Cannot train without data.");
        }

        Directory.CreateDirectory(config.OutputDir);

        // If a python script is explicitly provided and exists, execute external sidecar process
        if (!string.IsNullOrEmpty(pythonScriptPath) && File.Exists(pythonScriptPath))
        {
            return await RunPythonSidecarAsync(config, dataset, pythonScriptPath, pythonExecutable ?? "python", progress, cancellationToken);
        }

        // Default: Execute deterministic in-process PureEngineTrainer
        Action<TrainingProgressUpdate>? handler = null;
        if (progress != null)
        {
            handler = u => progress.Report(u);
            _pureEngineTrainer.ProgressUpdated += handler;
        }

        try
        {
            if (config.TaskType == DeepLearningTaskType.AnomalyDetection)
            {
                var (modelPath, anomalyMetrics) = await _pureEngineTrainer.TrainAnomalyModelAsync(config, dataset, "good", cancellationToken);
                var (meta, _) = ModelPackage.Load(modelPath);
                return new TrainingResult(true, modelPath, meta, anomalyMetrics);
            }
            else
            {
                // Classification or default
                var (modelPath, classMetrics) = await _pureEngineTrainer.TrainClassifierAsync(config, dataset, cancellationToken);
                var (meta, _) = ModelPackage.Load(modelPath);
                return new TrainingResult(true, modelPath, meta, classMetrics);
            }
        }
        catch (Exception ex)
        {
            return new TrainingResult(false, string.Empty, null, null, ex.Message);
        }
        finally
        {
            if (handler != null)
            {
                _pureEngineTrainer.ProgressUpdated -= handler;
            }
        }
    }

    private async Task<TrainingResult> RunPythonSidecarAsync(
        TrainingConfig config,
        DatasetManager dataset,
        string scriptPath,
        string pythonExe,
        IProgress<TrainingProgressUpdate>? progress,
        CancellationToken ct)
    {
        string configFile = Path.Combine(config.OutputDir, "train_config.json");
        string configJson = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(configFile, configJson, ct);

        var startInfo = new ProcessStartInfo
        {
            FileName = pythonExe,
            Arguments = $"\"{scriptPath}\" --config \"{configFile}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = config.OutputDir
        };

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            return new TrainingResult(false, string.Empty, null, null, $"Failed to launch Python sidecar: {ex.Message}");
        }

        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        while (!process.StandardOutput.EndOfStream)
        {
            if (ct.IsCancellationRequested)
            {
                try { process.Kill(true); } catch { }
                return new TrainingResult(false, string.Empty, null, null, "Training cancelled by user.");
            }

            string? line = await process.StandardOutput.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (line.StartsWith("{") && line.EndsWith("}"))
            {
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    int epoch = root.TryGetProperty("epoch", out var ep) ? ep.GetInt32() : 0;
                    int totalEpochs = root.TryGetProperty("total_epochs", out var tep) ? tep.GetInt32() : config.Epochs;
                    int step = root.TryGetProperty("step", out var st) ? st.GetInt32() : 0;
                    int totalSteps = root.TryGetProperty("total_steps", out var tst) ? tst.GetInt32() : 100;
                    double loss = root.TryGetProperty("loss", out var ls) ? ls.GetDouble() : 0.0;
                    double metric = root.TryGetProperty("val_metric", out var vm) ? vm.GetDouble() : 0.0;
                    string msg = root.TryGetProperty("message", out var ms) ? ms.GetString() ?? "" : "";

                    progress?.Report(new TrainingProgressUpdate(epoch, totalEpochs, step, totalSteps, loss, metric, msg));
                }
                catch
                {
                    // Ignore non-json lines
                }
            }
        }

        await process.WaitForExitAsync(ct);
        string stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            return new TrainingResult(false, string.Empty, null, null, $"Python sidecar exited with code {process.ExitCode}: {stderr}");
        }

        string expectedModel = Path.Combine(config.OutputDir, $"{config.ModelName}.vsmodel");
        if (!File.Exists(expectedModel))
        {
            return new TrainingResult(false, string.Empty, null, null, $"Training completed but expected package '{expectedModel}' was not found.");
        }

        var (meta, _) = ModelPackage.Load(expectedModel);
        return new TrainingResult(true, expectedModel, meta, null);
    }
}
