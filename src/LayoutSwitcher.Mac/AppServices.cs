using System.Threading.Channels;
using LayoutSwitcher.Core.Config;
using LayoutSwitcher.Core.Input;
using LayoutSwitcher.Mac.Engine;
using LayoutSwitcher.Mac.Input;
using LayoutSwitcher.Mac.Layouts;

namespace LayoutSwitcher.Mac;

/// <summary>Event tap + engine wired together.</summary>
internal sealed class AppServices : IDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _engineTask;

    private AppServices(LayoutService layouts, IEngineHost host)
    {
        var channel = Channel.CreateUnbounded<InputEvent>(new UnboundedChannelOptions { SingleReader = true });
        Layouts = layouts;
        Tap = new EventTap(channel.Writer);
        Engine = new SwitchEngine(channel.Reader, layouts, host, TimeProvider.System);
        Tap.Start();
        _engineTask = Task.Run(() => Engine.RunAsync(_stop.Token));
    }

    public LayoutService Layouts { get; }
    public EventTap Tap { get; }
    public SwitchEngine Engine { get; }

    /// <summary>Throws when the event tap cannot be created (no Accessibility permission).</summary>
    public static AppServices Start(LayoutService layouts, IEngineHost host, Settings settings, out List<string> invalidHotkeys)
    {
        var services = new AppServices(layouts, host);
        services.Apply(settings, out invalidHotkeys);
        return services;
    }

    /// <summary>Pushes settings into the running tap and engine.</summary>
    public void Apply(Settings settings, out List<string> invalidHotkeys)
    {
        Tap.Hotkeys = HotkeyMap.From(settings.Hotkeys, out invalidHotkeys);
        Tap.Enabled = settings.Enabled;
        Engine.ExtraWordChars = settings.ExtraWordChars;
    }

    public void Dispose()
    {
        Tap.Dispose();
        _stop.Cancel();
        try
        {
            _engineTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException e) when (e.InnerException is OperationCanceledException)
        {
        }
        _stop.Dispose();
    }
}
