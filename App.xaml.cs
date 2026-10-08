using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Reclaim.Core;
using Reclaim.Ui;

namespace Reclaim;

public partial class App : Application
{
    const string InstanceName = @"Local\Reclaim.Instance";
    const string ActivateEventName = @"Local\Reclaim.Activate";

    Mutex? instanceMutex;
    bool ownsMutex;
    EventWaitHandle? activateEvent;
    RegisteredWaitHandle? activateWait;
    MainWindow? mainWindow;
    TrayController? tray;

    public static new App Current => (App)Application.Current;

    public ReclaimService Service { get; private set; } = null!;
    public TrayController Tray => tray!;
    public bool IsExiting { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var culture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        instanceMutex = new Mutex(true, InstanceName, out ownsMutex);
        if (!ownsMutex)
        {
            if (EventWaitHandle.TryOpenExisting(ActivateEventName, out var existing))
            {
                existing.Set();
                existing.Dispose();
            }
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnUnhandledException;
        activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
        activateWait = ThreadPool.RegisterWaitForSingleObject(activateEvent, (_, _) => Dispatcher.BeginInvoke(ShowMainWindow), null, Timeout.Infinite, false);

        Privileges.EnableMemoryPrivileges();
        var settings = SettingsStore.Load();
        Service = new ReclaimService(settings);
        Service.Cleaned += OnCleaned;
        tray = new TrayController(this, Service);
        mainWindow = new MainWindow(Service);

        bool launchedAtSignIn = e.Args.Any(a => a.Equals(StartupManager.StartupArgument, StringComparison.OrdinalIgnoreCase));
        if (!(launchedAtSignIn && settings.StartSilently))
            mainWindow.Show();

        Service.Start();
        _ = SyncStartupTaskAsync();
    }

    public void ShowMainWindow()
    {
        if (mainWindow is null || IsExiting)
            return;
        if (!mainWindow.IsVisible)
            mainWindow.Show();
        if (mainWindow.WindowState == WindowState.Minimized)
            mainWindow.WindowState = WindowState.Normal;
        mainWindow.Activate();
        mainWindow.Topmost = true;
        mainWindow.Topmost = false;
        mainWindow.Focus();
    }

    public async Task CleanFromTrayAsync()
    {
        var result = await Service.CleanAsync(CleanTrigger.Manual);
        if (result is not null && mainWindow is { IsVisible: false })
            tray?.Notify(Headline(result), $"{MainViewModel.Describe(result.Performed)} · {Format.Bytes(result.After.Free)} free now");
    }

    public void ExitApplication()
    {
        if (IsExiting)
            return;
        IsExiting = true;
        Service.Stop();
        tray?.Dispose();
        Shutdown();
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        IsExiting = true;
        base.OnSessionEnding(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        tray?.Dispose();
        activateWait?.Unregister(null);
        activateEvent?.Dispose();
        if (ownsMutex)
            instanceMutex?.ReleaseMutex();
        instanceMutex?.Dispose();
        base.OnExit(e);
    }

    void OnCleaned(object? sender, CleanCompletedEventArgs e)
    {
        if (e.Trigger == CleanTrigger.Manual || !Service.Settings.Notifications)
            return;
        string reason = e.Trigger == CleanTrigger.Schedule ? "Scheduled clean" : "Memory limit reached";
        tray?.Notify(Headline(e.Result), $"{reason} · {Format.Bytes(e.Result.After.Free)} free now");
    }

    static string Headline(CleanResult result) =>
        result.FreedBytes >= 1024 * 1024 ? $"Reclaimed {Format.Bytes(result.FreedBytes)}" : "Memory is already optimal";

    async Task SyncStartupTaskAsync()
    {
        var settings = Service.Settings;
        bool pointsHere = false;
        bool registered = await Task.Run(() => StartupManager.IsRegistered(out pointsHere));

        if (registered && !settings.RunAtStartup)
        {
            settings.RunAtStartup = true;
            SettingsStore.Save(settings);
            mainWindow?.ViewModel.NotifyStartupChanged();
        }

        if (settings.RunAtStartup && (!registered || !pointsHere))
        {
            try
            {
                await Task.Run(StartupManager.Register);
            }
            catch (InvalidOperationException)
            {
            }
        }
    }

    void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(SettingsStore.Folder);
            File.AppendAllText(Path.Combine(SettingsStore.Folder, "error.log"), $"[{DateTime.Now:u}] {e.Exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (IOException)
        {
        }
        e.Handled = true;
    }
}
