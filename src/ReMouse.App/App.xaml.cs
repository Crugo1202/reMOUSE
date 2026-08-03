using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace ReMouse.App;

public partial class App : Application
{
    private const string SingleInstanceMutexName = "Local\\reMOUSE.App.SingleInstance";
    private const string ActivationEventName = "Local\\reMOUSE.App.Activate";
    private const string PrimaryClosingEventName = "Local\\reMOUSE.App.PrimaryClosing";

    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _activationEvent;
    private EventWaitHandle? _primaryClosingEvent;
    private RegisteredWaitHandle? _activationRegistration;
    private bool _ownsSingleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _activationEvent = new EventWaitHandle(
            initialState: false,
            EventResetMode.AutoReset,
            ActivationEventName);
        _primaryClosingEvent = new EventWaitHandle(
            initialState: false,
            EventResetMode.ManualReset,
            PrimaryClosingEventName);
        _singleInstanceMutex = new Mutex(
            initiallyOwned: true,
            SingleInstanceMutexName,
            out var createdNew);

        if (!createdNew)
        {
            _activationEvent.Set();
            try
            {
                // The primary may be in the middle of closing. Recheck the
                // mutex for a bounded interval so this process can take over
                // instead of letting both instances exit with no owner.
                var acquired = false;
                if (_primaryClosingEvent.WaitOne(0))
                {
                    acquired = WaitForMutexAfterPrimaryClosing();
                }
                else
                {
                    for (var attempt = 0; attempt < 10 && !acquired; attempt++)
                    {
                        try
                        {
                            acquired = _singleInstanceMutex.WaitOne(TimeSpan.FromMilliseconds(100));
                        }
                        catch (AbandonedMutexException)
                        {
                            acquired = true;
                        }

                        if (!acquired && _primaryClosingEvent.WaitOne(0))
                        {
                            acquired = WaitForMutexAfterPrimaryClosing();
                        }
                    }
                }

                if (!acquired)
                {
                    Shutdown();
                    return;
                }

                _activationEvent.Reset();
                _primaryClosingEvent.Reset();
            }
            catch
            {
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
                throw;
            }
        }

        _ownsSingleInstanceMutex = true;
        _primaryClosingEvent.Reset();
        base.OnStartup(e);

        var window = new MainWindow();
        MainWindow = window;
        _activationRegistration = ThreadPool.RegisterWaitForSingleObject(
            _activationEvent,
            static (state, _) =>
            {
                var app = (App)state!;
                app.Dispatcher.BeginInvoke(
                    DispatcherPriority.Normal,
                    new Action(() => (app.MainWindow as MainWindow)?.ActivateFromExternalLaunch()));
            },
            this,
            Timeout.Infinite,
            executeOnlyOnce: false);
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _activationRegistration?.Unregister(null);
        _activationRegistration = null;
        _activationEvent?.Dispose();
        _activationEvent = null;

        if (_ownsSingleInstanceMutex)
        {
            _primaryClosingEvent?.Set();
        }

        _primaryClosingEvent?.Dispose();
        _primaryClosingEvent = null;

        if (_ownsSingleInstanceMutex)
        {
            try
            {
                _singleInstanceMutex?.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // The process is already exiting; the named mutex will be
                // released by the OS when its owning handle closes.
            }
        }

        _singleInstanceMutex?.Dispose();
        _singleInstanceMutex = null;
        base.OnExit(e);
    }

    internal static void NotifyPrimaryClosing()
    {
        if (Current is App app && app._ownsSingleInstanceMutex)
        {
            app._primaryClosingEvent?.Set();
        }
    }

    private bool WaitForMutexAfterPrimaryClosing()
    {
        while (true)
        {
            try
            {
                if (_singleInstanceMutex!.WaitOne(TimeSpan.FromMilliseconds(100)))
                {
                    return true;
                }
            }
            catch (AbandonedMutexException)
            {
                return true;
            }

            // Another contender may have acquired and reset the closing
            // event. In that case this process must remain a secondary and
            // exit rather than waiting for the new primary's lifetime.
            if (!_primaryClosingEvent!.WaitOne(0))
            {
                return false;
            }
        }
    }
}
