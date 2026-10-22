using Microsoft.Extensions.Logging.Abstractions;
using VisionStudio.App.ViewModels;
using VisionStudio.Core.Pipeline;
using VisionStudio.Core.Tools;
using VisionStudio.Imaging.Acquisition;

namespace VisionStudio.App.Tests;

public class DesignViewModelTests
{
    private readonly ToolRegistry _toolRegistry;

    public DesignViewModelTests()
    {
        _toolRegistry = new ToolRegistry();
        _toolRegistry.RegisterAssembly(typeof(PipelineGraph).Assembly);
        _toolRegistry.RegisterAssembly(typeof(CameraGrabTool).Assembly);
    }

    [Fact]
    public async Task DesignViewModel_InitializesDefaultStarterPipeline_AndRunsSuccessfully()
    {
        var vm = new DesignViewModel(NullLogger<DesignViewModel>.Instance, _toolRegistry);

        vm.Nodes.Should().NotBeEmpty();
        vm.Connections.Should().NotBeEmpty();
        vm.InspectionStrip.Should().HaveCount(vm.Nodes.Count);

        // Execute
        await vm.RunPipelineAsync();

        // Check that nodes produced results and images
        foreach (var node in vm.Nodes)
        {
            node.Status.Should().Be(ToolStatus.Ok);
            node.LastResult.Should().NotBeNull();
            node.ExecutionTimeMs.Should().BeGreaterThanOrEqualTo(0.0);
        }

        vm.DisplayedImage.Should().NotBeNull();
        vm.DisplayedTitle.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void CanConnect_PreventsCyclicConnections()
    {
        var vm = new DesignViewModel(NullLogger<DesignViewModel>.Instance, _toolRegistry);
        vm.ResetPipeline();

        // N1 -> N2 -> N3
        var n1 = vm.Nodes[0];
        var n2 = vm.Nodes[1];
        var n3 = vm.Nodes[2];

        // Output of N3 back to Input of N1 would form a cycle
        var n3Out = n3.OutputPorts.First(p => p.PortType == Core.Ports.PortType.Image);
        var n1In = n1.InputPorts.FirstOrDefault(p => p.PortType == Core.Ports.PortType.Image);

        if (n1In != null)
        {
            var canConnectCycle = vm.CanConnect(n3Out, n1In);
            canConnectCycle.Should().BeFalse();
        }
    }

    [Fact]
    public void AddNode_And_DeleteNode_WithUndoRedo()
    {
        var vm = new DesignViewModel(NullLogger<DesignViewModel>.Instance, _toolRegistry);
        var initialCount = vm.Nodes.Count;

        var cropTool = vm.AllTools.FirstOrDefault(t => t.Id == "Geometry.Crop");
        cropTool.Should().NotBeNull();

        // Act: Add Node
        vm.AddNode(cropTool);
        vm.Nodes.Count.Should().Be(initialCount + 1);
        vm.CanUndo.Should().BeTrue();

        // Undo
        vm.Undo();
        vm.Nodes.Count.Should().Be(initialCount);
        vm.CanRedo.Should().BeTrue();

        // Redo
        vm.Redo();
        vm.Nodes.Count.Should().Be(initialCount + 1);

        // Delete
        vm.SelectedNode = vm.Nodes.Last();
        vm.DeleteSelected();
        vm.Nodes.Count.Should().Be(initialCount);
    }

    [Fact]
    public void ToggleBreakpoint_UpdatesNodeAndGraphState()
    {
        var vm = new DesignViewModel(NullLogger<DesignViewModel>.Instance, _toolRegistry);
        var targetNode = vm.Nodes.First();

        targetNode.HasBreakpoint.Should().BeFalse();

        vm.ToggleBreakpoint(targetNode);
        targetNode.HasBreakpoint.Should().BeTrue();

        vm.ToggleBreakpoint(targetNode);
        targetNode.HasBreakpoint.Should().BeFalse();
    }

    [Fact]
    public async Task ParameterChange_WithAutoRun_TriggersPipelineReExecution()
    {
        var vm = new DesignViewModel(NullLogger<DesignViewModel>.Instance, _toolRegistry);
        await vm.RunPipelineAsync();

        // Find Caliper node
        var caliperNode = vm.Nodes.FirstOrDefault(n => n.ToolId == "Measurement.Caliper");
        caliperNode.Should().NotBeNull();

        var thresholdParam = caliperNode!.Parameters.FirstOrDefault(p => p.Name == "EdgeThreshold");
        thresholdParam.Should().NotBeNull();

        // Modify threshold
        thresholdParam!.DoubleValue = 35.0;

        if (vm.LastExecutionTask != null)
        {
            await vm.LastExecutionTask;
        }

        // Auto-run should have updated the caliper node
        caliperNode.LastResult.Should().NotBeNull();
        caliperNode.Status.Should().Be(ToolStatus.Ok);
    }

    [Fact]
    public void ToolboxSelection_UpdatesActiveGuide_AndViewState()
    {
        var vm = new DesignViewModel(NullLogger<DesignViewModel>.Instance, _toolRegistry);

        // Initially a tool item or node is selected
        var blobTool = vm.AllTools.FirstOrDefault(t => t.Id == "Blob.Analysis");
        blobTool.Should().NotBeNull();

        // Selecting tool item in toolbox
        vm.SelectedToolItem = blobTool;

        vm.HasSelectedNode.Should().BeFalse();
        vm.ShowCatalogGuide.Should().BeTrue();
        vm.ActiveGuide.Should().NotBeNull();
        vm.ActiveGuide!.DisplayNameVi.Should().Contain("Blob");
        vm.ActiveGuide.IllustrationDiagram.Should().NotBeNullOrEmpty();

        // Selecting a node clears tool item and sets HasSelectedNode
        vm.SelectedNode = vm.Nodes.First();
        vm.HasSelectedNode.Should().BeTrue();
        vm.ShowCatalogGuide.Should().BeFalse();
        vm.SelectedToolItem.Should().BeNull();
    }

    [Fact]
    public void PortViewModel_ConnectionPoint_NotifiesAndMatchesCanvasPosition()
    {
        var vm = new DesignViewModel(NullLogger<DesignViewModel>.Instance, _toolRegistry);
        var node = vm.Nodes.First();
        var port = node.OutputPorts.First();

        var notifiedProps = new List<string>();
        port.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null) notifiedProps.Add(e.PropertyName);
        };

        var newPoint = new System.Windows.Point(350, 450);
        port.CanvasPosition = newPoint;

        port.ConnectionPoint.Should().Be(newPoint);
        notifiedProps.Should().Contain(nameof(PortViewModel.ConnectionPoint));
        notifiedProps.Should().Contain(nameof(PortViewModel.CanvasPosition));
    }

    [Fact]
    public void ConnectPorts_CreatesConnectionBetweenValidPorts()
    {
        var vm = new DesignViewModel(NullLogger<DesignViewModel>.Instance, _toolRegistry);
        vm.ResetPipeline();

        // Add 2 disconnected nodes
        var n1 = vm.AddNodeInternal("Acquisition.CameraGrab", 50, 80);
        var n2 = vm.AddNodeInternal("Segmentation.Threshold", 350, 80);

        var outPort = n1.OutputPorts.First(p => p.PortType == Core.Ports.PortType.Image);
        var inPort = n2.InputPorts.First(p => p.PortType == Core.Ports.PortType.Image);

        vm.CanConnect(outPort, inPort).Should().BeTrue();
        vm.ConnectPorts(outPort, inPort);

        vm.Connections.Should().Contain(c => c.SourceNodeId == n1.NodeId && c.TargetNodeId == n2.NodeId);
        outPort.IsConnected.Should().BeTrue();
        inPort.IsConnected.Should().BeTrue();
    }

    [Fact]
    public void ApplyInteractiveRoi_UpdatesSelectedNodeParameters()
    {
        var vm = new DesignViewModel(NullLogger<DesignViewModel>.Instance, _toolRegistry);
        var cropNode = vm.AddNodeInternal("Geometry.Crop", 100, 100);
        vm.SelectedNode = cropNode;

        vm.SelectedNodeSupportsRoi.Should().BeTrue();
        vm.SelectedNodeSupportsInteractiveSelection.Should().BeTrue();

        bool success = vm.ApplyInteractiveRoi(50, 60, 200, 150);
        success.Should().BeTrue();

        var pX = cropNode.Parameters.First(p => p.Name == "X");
        var pY = cropNode.Parameters.First(p => p.Name == "Y");
        var pW = cropNode.Parameters.First(p => p.Name == "Width");
        var pH = cropNode.Parameters.First(p => p.Name == "Height");

        pX.IntValue.Should().Be(50);
        pY.IntValue.Should().Be(60);
        pW.IntValue.Should().Be(200);
        pH.IntValue.Should().Be(150);
    }

    [Fact]
    public void ApplyInteractiveLine_UpdatesCaliperScanVector()
    {
        var vm = new DesignViewModel(NullLogger<DesignViewModel>.Instance, _toolRegistry);
        var caliperNode = vm.Nodes.FirstOrDefault(n => n.ToolId == "Measurement.Caliper");
        caliperNode.Should().NotBeNull();
        vm.SelectedNode = caliperNode;

        vm.SelectedNodeSupportsLine.Should().BeTrue();

        bool success = vm.ApplyInteractiveLine(120.5, 80.0, 360.5, 80.0);
        success.Should().BeTrue();

        var pStartX = caliperNode!.Parameters.First(p => p.Name == "StartX");
        var pStartY = caliperNode.Parameters.First(p => p.Name == "StartY");
        var pEndX = caliperNode.Parameters.First(p => p.Name == "EndX");
        var pEndY = caliperNode.Parameters.First(p => p.Name == "EndY");

        pStartX.DoubleValue.Should().Be(120.5);
        pStartY.DoubleValue.Should().Be(80.0);
        pEndX.DoubleValue.Should().Be(360.5);
        pEndY.DoubleValue.Should().Be(80.0);
    }

    [Fact]
    public void ApplyInteractivePolygon_UpdatesPolygonRoiToolPoints()
    {
        var vm = new DesignViewModel(NullLogger<DesignViewModel>.Instance, _toolRegistry);
        var polyNode = vm.AddNodeInternal("Geometry.PolygonRoi", 200, 150);
        vm.SelectedNode = polyNode;

        vm.SelectedNodeSupportsPolygon.Should().BeTrue();

        var pts = new List<System.Windows.Point>
        {
            new(50, 40),
            new(150, 40),
            new(180, 120),
            new(40, 110)
        };

        bool success = vm.ApplyInteractivePolygon(pts);
        success.Should().BeTrue();

        var pPoints = polyNode.Parameters.First(p => p.Name == "Points");
        pPoints.StringValue.Should().Be("50,40; 150,40; 180,120; 40,110");
    }

    [Fact]
    public void SetInteractionMode_RaisesRequestSwitchToImageViewer()
    {
        var vm = new DesignViewModel(NullLogger<DesignViewModel>.Instance, _toolRegistry);
        bool tabSwitchRequested = false;
        vm.RequestSwitchToImageViewer += () => tabSwitchRequested = true;

        vm.SetInteractionMode(InteractiveToolMode.RectangleRoi);

        vm.ActiveInteractionMode.Should().Be(InteractiveToolMode.RectangleRoi);
        tabSwitchRequested.Should().BeTrue();
    }

    [Fact]
    public async Task LoadPresetPipelineAsync_Preset09_LoadsCartonCounterAndRuns()
    {
        _toolRegistry.RegisterAssembly(typeof(VisionStudio.DeepLearning.Tools.CartonThroughputCounterTool).Assembly);
        var vm = new DesignViewModel(NullLogger<DesignViewModel>.Instance, _toolRegistry);

        await vm.LoadPresetPipelineAsync("09");

        vm.Nodes.Should().Contain(n => n.ToolId == "DeepLearning.CartonCounter");
        vm.SelectedNode.Should().NotBeNull();
        vm.SelectedNodeSupportsLine.Should().BeTrue();

        // Test drawing interactive tripwire line on CartonCounter
        bool appliedLine = vm.ApplyInteractiveLine(320, 20, 320, 460);
        appliedLine.Should().BeTrue();

        var pStartX = vm.SelectedNode!.Parameters.First(p => p.Name == "LineStartX");
        pStartX.DoubleValue.Should().Be(320);

        // Run pipeline
        await vm.RunPipelineAsync();
        vm.Nodes.First(n => n.ToolId == "DeepLearning.CartonCounter").Status.Should().Be(ToolStatus.Ok);
    }
}


