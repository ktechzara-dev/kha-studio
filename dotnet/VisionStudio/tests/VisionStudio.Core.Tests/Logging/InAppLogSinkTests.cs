using VisionStudio.Core.Logging;

namespace VisionStudio.Core.Tests.Logging;

public class InAppLogSinkTests
{
    [Fact]
    public void Emit_DispatchesEventAndStoresEntry()
    {
        // Arrange
        var sink = new InAppLogSink(maxCapacity: 10);
        LogEntry? captured = null;
        sink.LogEmitted += entry => captured = entry;

        var testEntry = new LogEntry(
            Timestamp: DateTimeOffset.UtcNow,
            Level: LogLevel.Information,
            Source: "Tests",
            Message: "Pipeline started");

        // Act
        sink.Emit(testEntry);

        // Assert
        captured.Should().NotBeNull();
        captured!.Message.Should().Be("Pipeline started");
        sink.GetRecentEntries().Should().ContainSingle().Which.Message.Should().Be("Pipeline started");
    }

    [Fact]
    public void Emit_RespectsMaxCapacityByDroppingOldest()
    {
        // Arrange
        var sink = new InAppLogSink(maxCapacity: 3);

        // Act
        for (int i = 1; i <= 5; i++)
        {
            sink.Emit(new LogEntry(
                Timestamp: DateTimeOffset.UtcNow,
                Level: LogLevel.Debug,
                Source: "Test",
                Message: $"Message {i}"));
        }

        // Assert
        var entries = sink.GetRecentEntries();
        entries.Should().HaveCount(3);
        entries.Select(e => e.Message).Should().ContainInOrder("Message 3", "Message 4", "Message 5");
    }

    [Fact]
    public void Clear_EmptiesAllCachedEntries()
    {
        // Arrange
        var sink = new InAppLogSink();
        sink.Emit(new LogEntry(DateTimeOffset.UtcNow, LogLevel.Information, "Test", "Msg"));

        // Act
        sink.Clear();

        // Assert
        sink.GetRecentEntries().Should().BeEmpty();
    }
}
