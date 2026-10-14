using System.Windows;
using VisionStudio.App.Controls;
using VisionStudio.Core.Pipeline;
using VisionStudio.Core.Traceability;
using VisionStudio.DeepLearning.Datasets;
using VisionStudio.Core.Recipes;
using Xunit;

namespace VisionStudio.App.Tests;

public class WindowInstantiationTests
{
    private static void RunOnSta(Action action)
    {
        Exception? ex = null;
        var thread = new Thread(() =>
        {
            try
            {
                if (Application.Current == null)
                {
                    var app = new Application();
                    app.Resources.MergedDictionaries.Add(new ResourceDictionary
                    {
                        Source = new Uri("pack://application:,,,/VisionStudio.App;component/Themes/KhaStudioDarkTheme.xaml", UriKind.RelativeOrAbsolute)
                    });
                }
                action();
            }
            catch (Exception e)
            {
                ex = e;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (ex != null)
        {
            throw new AggregateException("STA Thread Exception", ex);
        }
    }

    [Fact]
    public void AllWindows_InstantiateWithoutXamlOrRuntimeExceptions()
    {
        RunOnSta(() =>
        {
            var graph = new PipelineGraph();

            // 1. HMI Designer
            var hmi = new HmiDesignerWindow();
            Assert.NotNull(hmi);

            // 2. Deep Learning Workspace
            var dl = new DeepLearningWorkspaceWindow();
            Assert.NotNull(dl);

            // 3. Recipe Manager
            var recipes = new RecipeManagerWindow(new RecipeManager(), graph);
            Assert.NotNull(recipes);

            // 4. Traceability History
            var trace = new TraceabilityHistoryWindow(new TraceabilityStore(), graph);
            Assert.NotNull(trace);

            // 5. Industrial Connectivity
            var conn = new IndustrialConnectivityWindow();
            Assert.NotNull(conn);

            // 6. Geometric Model Editor
            var geo = new GeometricModelEditorWindow();
            Assert.NotNull(geo);

            // 7. Coordinate Frames Tree
            var frames = new CoordinateFrameTreeWindow();
            Assert.NotNull(frames);

            // 8. Auto-Tune Window
            var autoTune = new AutoTuneWindow(graph, null);
            Assert.NotNull(autoTune);

            // 9. State Machine Editor
            var sm = new StateMachineEditorWindow();
            Assert.NotNull(sm);

            // 10. Macro Library
            var macro = new MacroLibraryWindow();
            Assert.NotNull(macro);

            // 11. Quick Build Wizards
            var wizard = new QuickBuildWizardWindow();
            Assert.NotNull(wizard);

            // 12. Tool Compare
            var compare = new ToolCompareWindow();
            Assert.NotNull(compare);

            // 13. Command Palette
            var palette = new CommandPaletteWindow(new List<CommandPaletteItem>());
            Assert.NotNull(palette);

            // 14. Tool Guide
            var guide = new ToolGuideWindow(new VisionStudio.Core.Tools.ToolRegistry());
            Assert.NotNull(guide);
        });
    }
}
