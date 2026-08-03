using System.Threading.Channels;
using ReMouse.Core.Input;

namespace ReMouse.App;

/// <summary>
/// Applies queued middle-chord effects away from WH_MOUSE_LL. A sink failure
/// closes the bridge through the pump's finally path so the hook fails open.
/// </summary>
public sealed class MiddleChordEffectPump
{
    private readonly IInputEffectSink _effectSink;
    private readonly MiddleChordInputBridge? _inputBridge;

    public MiddleChordEffectPump(
        IInputEffectSink effectSink,
        MiddleChordInputBridge? inputBridge = null)
    {
        _effectSink = effectSink ?? throw new ArgumentNullException(nameof(effectSink));
        _inputBridge = inputBridge;
    }

    public async Task RunAsync(
        IAsyncEnumerable<InputEffect> effects,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(effects);

        var syntheticMiddleDownOutstanding = false;
        try
        {
            await foreach (var effect in effects.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                if (effect is InputEffect.MiddleDragReady ready)
                {
                    // The marker is ordered after the synthetic Down and all
                    // replayed moves. Only now may the hook pass native moves.
                    _inputBridge?.MarkDragReady(ready.SequenceId);
                    continue;
                }

                if (effect is InputEffect.MiddleButtonDown)
                {
                    // Set ownership before Apply: a failing SendInput call may
                    // have partially delivered the Down.
                    syntheticMiddleDownOutstanding = true;
                }

                _effectSink.Apply(effect);

                if (effect is InputEffect.MiddleButtonUp)
                {
                    syntheticMiddleDownOutstanding = false;
                }
            }
        }
        finally
        {
            if (syntheticMiddleDownOutstanding)
            {
                try
                {
                    _effectSink.Apply(new InputEffect.MiddleButtonUp());
                }
                catch
                {
                    // Best effort only: the sink has already faulted or the
                    // process is shutting down, but never leave ownership
                    // unaccounted for in the pump.
                }
            }

            _inputBridge?.CompleteAfterPump();
        }
    }

    /// <summary>
    /// Consumes the bridge's channel directly so contiguous replay moves that
    /// are already buffered can be coalesced to the latest coordinate. This
    /// never waits in the hook callback and never drops a button boundary: a
    /// synthetic Down/Up remains ordered, while stale move/marker pairs are
    /// safe to skip because only the newest marker can unlock native motion.
    /// </summary>
    public async Task RunAsync(
        ChannelReader<InputEffect> effects,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(effects);

        var syntheticMiddleDownOutstanding = false;
        try
        {
            while (await effects.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                while (effects.TryRead(out var effect))
                {
                    if (effect is InputEffect.MouseMove move)
                    {
                        ApplyCoalescedReplay(
                            effects,
                            move,
                            ref syntheticMiddleDownOutstanding);
                        continue;
                    }

                    ApplyEffect(effect, ref syntheticMiddleDownOutstanding);
                }
            }
        }
        finally
        {
            ReleaseOutstandingMiddleButton(syntheticMiddleDownOutstanding);
            _inputBridge?.CompleteAfterPump();
        }
    }

    private void ApplyCoalescedReplay(
        ChannelReader<InputEffect> effects,
        InputEffect.MouseMove firstMove,
        ref bool syntheticMiddleDownOutstanding)
    {
        var latestMove = firstMove;
        long? latestMarker = null;

        while (effects.TryRead(out var next))
        {
            if (next is InputEffect.MiddleDragReady ready)
            {
                // A marker acknowledges the move immediately before it. If a
                // newer move follows, this token becomes stale and is replaced
                // by that move's marker below.
                latestMarker = ready.SequenceId;
                continue;
            }

            if (next is InputEffect.MouseMove newerMove && latestMarker is not null)
            {
                latestMove = newerMove;
                latestMarker = null;
                continue;
            }

            // The next effect is a button boundary or another effect type.
            // Flush the newest replay point before it so ordering is preserved,
            // then process the effect without losing it.
            ApplyReplayMove(latestMove);
            if (latestMarker is { } marker)
            {
                _inputBridge?.MarkDragReady(marker);
            }

            ApplyEffect(next, ref syntheticMiddleDownOutstanding);
            return;
        }

        ApplyReplayMove(latestMove);
        if (latestMarker is { } finalMarker)
        {
            _inputBridge?.MarkDragReady(finalMarker);
        }
    }

    private void ApplyEffect(InputEffect effect, ref bool syntheticMiddleDownOutstanding)
    {
        if (effect is InputEffect.MiddleDragReady ready)
        {
            _inputBridge?.MarkDragReady(ready.SequenceId);
            return;
        }

        if (effect is InputEffect.MiddleButtonDown)
        {
            // Set ownership before Apply: a failing SendInput call may have
            // partially delivered the Down.
            syntheticMiddleDownOutstanding = true;
        }

        _effectSink.Apply(effect);

        if (effect is InputEffect.MiddleButtonUp)
        {
            syntheticMiddleDownOutstanding = false;
        }
    }

    private void ApplyReplayMove(InputEffect.MouseMove move) => _effectSink.Apply(move);

    private void ReleaseOutstandingMiddleButton(bool outstanding)
    {
        if (!outstanding)
        {
            return;
        }

        try
        {
            _effectSink.Apply(new InputEffect.MiddleButtonUp());
        }
        catch
        {
            // Best effort only: the sink has already faulted or the process is
            // shutting down, but never leave ownership unaccounted for.
        }
    }
}
