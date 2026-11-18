using System.Threading.Channels;
using LayoutSwitcher.App.Engine;
using LayoutSwitcher.App.Input;
using LayoutSwitcher.App.Layouts;
using LayoutSwitcher.Core.Config;

namespace LayoutSwitcher.App;

/// <summary>Hook + engine wired together. Shared by the tray app and the end-to-end check.</summary>
internal sealed class AppServices : IDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _engineTask;

    private AppServices(LayoutService layouts, IEngineHost host)
    {
        var channel = Channel.CreateUnbounded<InputEvent>(new UnboundedChannelOptions { SingleReader = true });
        Layouts = layouts;
        Hook = new KeyboardHook(channel.Writer);
        Engine = new SwitchEngine(channel.Reader, Hook, layouts, host, TimeProvider.System);
        Hook.Start();
        _engineTask = Task.Run(() => Engine.RunAsync(_stop.Token));
    }

    public LayoutService Layouts { get; }
    public KeyboardHook Hook { get; }
    public SwitchEngine Engine { get; }

    public static AppServices Start(LayoutService layouts, IEngineHost host, Settings settings)
    {
        var services = new AppServices(layouts, host);
        services.Apply(settings, out _);
        return services;
    }

    /// <summary>Pushes settings into the running hook and engine.</summary>
    public void Apply(Settings settings, out List<string> invalidHotkeys)
    {
        Hook.Hotkeys = HotkeyMap.From(settings.Hotkeys, out invalidHotkeys);
        Hook.Exclusions = new ExclusionList(settings.ExcludedPrograms);
        Hook.Enabled = settings.Enabled;
        Hook.RefreshForeground();
        Engine.ExtraWordChars = settings.ExtraWordChars;
    }

    public void Dispose()
    {
        Hook.Dispose();
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
