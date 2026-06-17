using InstrumentControl.Core.Models;

namespace InstrumentControl.Core.Tests;

public class SequenceContextTests
{
    [Fact]
    public void SetVariable_GetVariable_RoundTrips()
    {
        var ctx = new SequenceContext { CancellationToken = default };
        ctx.SetVariable("x", 42.0);
        Assert.Equal(42.0, ctx.GetVariableAsDouble("x"));
    }

    [Fact]
    public void GetVariableAsDouble_MissingKey_ReturnsDefault()
    {
        var ctx = new SequenceContext { CancellationToken = default };
        Assert.Equal(99.0, ctx.GetVariableAsDouble("missing", 99.0));
    }

    [Fact]
    public async Task WaitIfPausedAsync_NotPaused_ReturnsImmediately()
    {
        var ctx = new SequenceContext { CancellationToken = default };
        var task = ctx.WaitIfPausedAsync();
        Assert.True(task.IsCompleted || task.Wait(100));
    }

    [Fact]
    public async Task WaitIfPausedAsync_Paused_BlocksUntilResume()
    {
        var cts = new CancellationTokenSource();
        var ctx = new SequenceContext { CancellationToken = cts.Token };

        ctx.SetPaused(true);
        var task = Task.Run(() => ctx.WaitIfPausedAsync());

        await Task.Delay(50);
        Assert.False(task.IsCompleted);

        ctx.SetPaused(false);
        await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task WaitIfPausedAsync_CancelWhilePaused_Throws()
    {
        var cts = new CancellationTokenSource();
        var ctx = new SequenceContext { CancellationToken = cts.Token };

        ctx.SetPaused(true);
        var task = Task.Run(() => ctx.WaitIfPausedAsync());

        await Task.Delay(30);
        cts.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(() => task.WaitAsync(TimeSpan.FromSeconds(2)));
    }
}
