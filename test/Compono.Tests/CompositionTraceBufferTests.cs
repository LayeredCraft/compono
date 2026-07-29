namespace Compono.Tests;

public sealed class CompositionTraceBufferTests
{
    [Fact]
    public void Slice_ReturnsOnlyAttemptsRecordedSinceTheCheckpoint()
    {
        var buffer = new CompositionTraceBuffer();
        buffer.Record(PipelineStage.ExactRegistration, CompositionAttemptOutcome.NotHandled);
        var checkpoint = buffer.Checkpoint;
        buffer.Record(PipelineStage.BuiltInProvider, CompositionAttemptOutcome.Success);

        var slice = buffer.Slice(checkpoint);

        slice.Should().Equal(new ProviderAttempt(PipelineStage.BuiltInProvider, CompositionAttemptOutcome.Success));
    }

    [Fact]
    public void Rewind_DiscardsEverythingRecordedSinceTheCheckpoint()
    {
        var buffer = new CompositionTraceBuffer();
        var checkpoint = buffer.Checkpoint;
        buffer.Record(PipelineStage.ExactRegistration, CompositionAttemptOutcome.NotHandled);
        buffer.Record(PipelineStage.BuiltInProvider, CompositionAttemptOutcome.Success);

        buffer.Rewind(checkpoint);

        buffer.Checkpoint.Should().Be(checkpoint);
        buffer.Slice(checkpoint).Should().BeEmpty();
    }

    [Fact]
    public void Record_GrowsPastItsInitialCapacity_WithoutLosingEarlierAttempts()
    {
        var buffer = new CompositionTraceBuffer();

        for (var i = 0; i < 64; i++)
            buffer.Record(PipelineStage.ExactRegistration, CompositionAttemptOutcome.NotHandled);

        buffer.Slice(0).Should().HaveCount(64);
    }
}
