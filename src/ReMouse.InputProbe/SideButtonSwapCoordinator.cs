using ReMouse.Core.Input;
using ReMouse.Core.Settings;

namespace ReMouse.InputProbe;

internal readonly record struct SideButtonSwapResult(
    bool Succeeded,
    SideButtonBindings Bindings,
    Exception? Error,
    Exception? RollbackError);

/// <summary>
/// Coordinates persistence and the ordered settings control message. The
/// mapper itself changes only when the processor consumes that message.
/// </summary>
internal sealed class SideButtonSwapCoordinator
{
    private readonly SideButtonMapper _mapper;
    private readonly ProbeEventChannel _events;
    private readonly Action<ReMouseSettings>? _save;
    private ReMouseSettings _settings;

    public SideButtonSwapCoordinator(
        SideButtonMapper mapper,
        ProbeEventChannel events,
        Action<ReMouseSettings>? save)
        : this(mapper, events, ReMouseSettings.CreateDefault(), save)
    {
    }

    public SideButtonSwapCoordinator(
        SideButtonMapper mapper,
        ProbeEventChannel events,
        ReMouseSettings settings,
        Action<ReMouseSettings>? save)
    {
        _mapper = mapper;
        _events = events;
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _save = save;
    }

    public async Task<SideButtonSwapResult> TrySwapAndSaveAsync()
    {
        var previous = _mapper.Bindings;
        var next = previous.Swap();
        var nextSettings = new ReMouseSettings(
            _settings.SchemaVersion,
            next,
            _settings.Flick,
            _settings.RadialMenu);
        var previousSettings = _settings;
        var saved = false;

        try
        {
            _save?.Invoke(nextSettings);
            saved = _save is not null;

            var applied = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            if (!_events.TryWrite(nextSettings, applied))
            {
                throw new InvalidOperationException("The input processor is no longer accepting commands.");
            }

            // No cancellation or arbitrary timeout is used after enqueue. The
            // processor must acknowledge this ordered control message before
            // the command is considered committed.
            await applied.Task.ConfigureAwait(false);
            _settings = nextSettings;
            return new SideButtonSwapResult(true, next, null, null);
        }
        catch (Exception exception)
        {
            Exception? rollbackError = null;
            if (saved && _save is not null)
            {
                try
                {
                    _save(previousSettings);
                }
                catch (Exception rollbackException)
                {
                    rollbackError = rollbackException;
                }
            }

            return new SideButtonSwapResult(false, previous, exception, rollbackError);
        }
    }
}
