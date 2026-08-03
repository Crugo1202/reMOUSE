using ReMouse.Core.Input;

namespace ReMouse.Windows.Input;

/// <summary>
/// Converts platform-neutral effects into one atomic SendInput batch.
/// This adapter is intentionally not wired into the low-level observation hook.
/// </summary>
public sealed class WindowsInputEffectSink : IInputEffectSink
{
    private readonly IWindowsInputSender _sender;

    public WindowsInputEffectSink()
        : this(new SendInputNativeSender())
    {
    }

    internal WindowsInputEffectSink(IWindowsInputSender sender)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    public void Apply(InputEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);

        switch (effect)
        {
            case InputEffect.HorizontalWheel wheel:
                _sender.Send(new[] { WindowsInputPacket.HorizontalWheel(wheel.Delta) });
                return;

            case InputEffect.MiddleClick:
                _sender.Send(new[]
                {
                    WindowsInputPacket.MiddleButton(isDown: true),
                    WindowsInputPacket.MiddleButton(isDown: false)
                });
                return;

            case InputEffect.MiddleButtonDown:
                _sender.Send(new[] { WindowsInputPacket.MiddleButton(isDown: true) });
                return;

            case InputEffect.MiddleButtonUp:
                _sender.Send(new[] { WindowsInputPacket.MiddleButton(isDown: false) });
                return;

            case InputEffect.MouseMove move:
                _sender.Send(new[] { WindowsInputPacket.MouseMove(move.X, move.Y) });
                return;

            case InputEffect.MiddleDragReady:
                // Ordering marker consumed by the bridge/pump; it does not
                // represent a native input packet.
                return;

            case InputEffect.KeySequence sequence:
                var packets = new WindowsInputPacket[sequence.Strokes.Count];
                for (var index = 0; index < sequence.Strokes.Count; index++)
                {
                    var stroke = sequence.Strokes[index];
                    packets[index] = WindowsInputPacket.Key(stroke.VirtualKey, stroke.IsDown);
                }

                _sender.Send(packets);
                return;

            default:
                throw new ArgumentOutOfRangeException(nameof(effect), effect, "Unsupported input effect.");
        }
    }
}
