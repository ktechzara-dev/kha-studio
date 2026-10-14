using VisionStudio.Core.Pipeline;

namespace VisionStudio.Core.Recipes;

/// <summary>
/// Manages industrial vision recipes (Jobs), runtime recipe switching,
/// pipeline parameter override application, diffing, and validation.
/// </summary>
public sealed class RecipeManager
{
    private readonly List<Recipe> _recipes = new();
    private Recipe? _activeRecipe;

    public IReadOnlyList<Recipe> Recipes => _recipes;
    public Recipe? ActiveRecipe => _activeRecipe;

    public event Action<Recipe?>? ActiveRecipeChanged;

    public RecipeManager()
    {
    }

    public void AddRecipe(Recipe recipe)
    {
        if (recipe == null) throw new ArgumentNullException(nameof(recipe));

        int existingIndex = _recipes.FindIndex(r => string.Equals(r.Id, recipe.Id, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
        {
            _recipes[existingIndex] = recipe;
        }
        else
        {
            _recipes.Add(recipe);
        }
    }

    public bool RemoveRecipe(string recipeId)
    {
        int removed = _recipes.RemoveAll(r => string.Equals(r.Id, recipeId, StringComparison.OrdinalIgnoreCase));
        if (removed > 0 && _activeRecipe != null && string.Equals(_activeRecipe.Id, recipeId, StringComparison.OrdinalIgnoreCase))
        {
            _activeRecipe = null;
            ActiveRecipeChanged?.Invoke(null);
        }
        return removed > 0;
    }

    public Recipe? GetRecipe(string recipeId)
    {
        return _recipes.FirstOrDefault(r => string.Equals(r.Id, recipeId, StringComparison.OrdinalIgnoreCase) ||
                                            string.Equals(r.ProductCode, recipeId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Validates whether a recipe's target nodes and parameters exist within the pipeline graph.
    /// </summary>
    public RecipeValidationResult ValidateRecipe(Recipe recipe, PipelineGraph graph)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        foreach (var (nodeId, overrides) in recipe.ParameterOverrides)
        {
            var node = graph.GetNode(nodeId);
            if (node == null)
            {
                errors.Add($"Recipe references NodeId '{nodeId}' which does not exist in pipeline.");
                continue;
            }

            foreach (var (paramName, val) in overrides)
            {
                var param = node.Tool.Parameters.FirstOrDefault(p => string.Equals(p.Name, paramName, StringComparison.OrdinalIgnoreCase));
                if (param == null)
                {
                    warnings.Add($"Node '{nodeId}' has no parameter named '{paramName}'.");
                }
            }
        }

        foreach (var (nodeId, modelPath) in recipe.ModelBindings)
        {
            var node = graph.GetNode(nodeId);
            if (node == null)
            {
                errors.Add($"Recipe references ModelBinding for NodeId '{nodeId}' which does not exist.");
            }
            else if (!string.IsNullOrEmpty(modelPath) && !File.Exists(modelPath))
            {
                warnings.Add($"Bound model file for Node '{nodeId}' does not exist on disk: {modelPath}");
            }
        }

        return new RecipeValidationResult(errors.Count == 0, errors, warnings);
    }

    /// <summary>
    /// Switches the active recipe and applies all parameter overrides and model bindings to the graph.
    /// </summary>
    public bool SwitchRecipe(string recipeId, PipelineGraph graph, bool force = false)
    {
        var recipe = GetRecipe(recipeId);
        if (recipe == null)
        {
            throw new KeyNotFoundException($"Recipe with ID or ProductCode '{recipeId}' not found.");
        }

        var validation = ValidateRecipe(recipe, graph);
        if (!validation.IsValid && !force)
        {
            throw new InvalidOperationException($"Cannot apply recipe '{recipe.Name}' due to validation errors:\n" +
                                                string.Join("\n", validation.Errors));
        }

        ApplyRecipe(recipe, graph);
        _activeRecipe = recipe;
        ActiveRecipeChanged?.Invoke(_activeRecipe);
        return true;
    }

    /// <summary>
    /// Directly applies a recipe's parameters to the live pipeline graph.
    /// </summary>
    public void ApplyRecipe(Recipe recipe, PipelineGraph graph)
    {
        foreach (var (nodeId, overrides) in recipe.ParameterOverrides)
        {
            var node = graph.GetNode(nodeId);
            if (node == null) continue;

            foreach (var (paramName, val) in overrides)
            {
                var param = node.Tool.Parameters.FirstOrDefault(p => string.Equals(p.Name, paramName, StringComparison.OrdinalIgnoreCase));
                if (param != null && val != null)
                {
                    try
                    {
                        param.Value = val;
                    }
                    catch
                    {
                        // Ignore incompatible conversions
                    }
                }
            }
            node.MarkDirty();
        }

        foreach (var (nodeId, modelPath) in recipe.ModelBindings)
        {
            var node = graph.GetNode(nodeId);
            if (node == null) continue;

            var modelParam = node.Tool.Parameters.FirstOrDefault(p =>
                string.Equals(p.Name, "ModelPath", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.Name, "TemplatePath", StringComparison.OrdinalIgnoreCase));

            if (modelParam != null)
            {
                modelParam.Value = modelPath;
                node.MarkDirty();
            }
        }
    }

    /// <summary>
    /// Computes a side-by-side parameter diff between two recipes.
    /// </summary>
    public IReadOnlyList<RecipeDiffRow> DiffRecipes(Recipe a, Recipe b)
    {
        var diffs = new List<RecipeDiffRow>();
        var allNodeIds = a.ParameterOverrides.Keys.Union(b.ParameterOverrides.Keys).ToList();

        foreach (var nodeId in allNodeIds)
        {
            a.ParameterOverrides.TryGetValue(nodeId, out var aParams);
            b.ParameterOverrides.TryGetValue(nodeId, out var bParams);

            var allParamNames = (aParams?.Keys ?? Enumerable.Empty<string>())
                .Union(bParams?.Keys ?? Enumerable.Empty<string>())
                .ToList();

            foreach (var paramName in allParamNames)
            {
                object? valA = null;
                object? valB = null;

                aParams?.TryGetValue(paramName, out valA);
                bParams?.TryGetValue(paramName, out valB);

                if (!Equals(valA, valB))
                {
                    diffs.Add(new RecipeDiffRow(nodeId, paramName, valA, valB));
                }
            }
        }

        return diffs;
    }

    public void ExportRecipe(string recipeId, string filePath)
    {
        var recipe = GetRecipe(recipeId)
            ?? throw new KeyNotFoundException($"Recipe '{recipeId}' not found.");
        recipe.Save(filePath);
    }

    public Recipe ImportRecipe(string filePath)
    {
        var recipe = Recipe.Load(filePath);
        AddRecipe(recipe);
        return recipe;
    }
}
