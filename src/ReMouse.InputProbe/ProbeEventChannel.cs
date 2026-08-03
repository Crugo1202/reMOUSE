using System.Threading.Channels;
using ReMouse.Core.Input;
using ReMouse.Core.Settings;

namespace ReMouse.InputProbe;

internal sealed class ProbeEventChannel : IDisposable
{
    private int _closed;

    private readonly Channel<ProbeWorkItem> _channel = Channel.CreateUnbounded<ProbeWorkItem>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            // Hook thread and console command thread are both producers. The
            // processor is the only place where work is applied.
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

    public bool TryWrite(ProbeEvent inputEvent) =>
        _channel.Writer.TryWrite(ProbeWorkItem.FromInput(inputEvent));

    public bool TryWrite(ReMouseSettings settings, TaskCompletionSource<bool> applied) =>
        _channel.Writer.TryWrite(ProbeWorkItem.FromSettings(settings, applied));

    public bool TryWrite(InputEffect effect) =>
        _channel.Writer.TryWrite(ProbeWorkItem.FromEffect(effect));

    public bool IsClosed => Volatile.Read(ref _closed) != 0;

    public IAsyncEnumerable<ProbeWorkItem> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);

    public void Complete()
    {
        Interlocked.Exchange(ref _closed, 1);
        _channel.Writer.TryComplete();
    }

    public void Fail(Exception exception)
    {
        Interlocked.Exchange(ref _closed, 1);
        _channel.Writer.TryComplete(exception);
        while (_channel.Reader.TryRead(out var workItem))
        {
            workItem.Applied?.TrySetException(exception);
        }
    }

    public void Dispose() => Complete();
}
