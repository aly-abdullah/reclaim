using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using Reclaim.Core;

namespace Reclaim;

public partial class TrayMenu : Window
{
    readonly ReclaimService service;
    bool closing;

    public TrayMenu(ReclaimService service)
    {
        InitializeComponent();
        this.service = service;
        Populate();
        ContentRendered += OnContentRendered;
        Deactivated += (_, _) => Dismiss();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Dismiss();
        };
    }

    void Populate()
    {
        var snapshot = service.Current;
        Ring.Value = snapshot.UsedRatio;
        PercentText.Text = $"{Math.Round(snapshot.UsedRatio * 100):0}% memory in use";
        DetailText.Text = $"Standby {Format.Bytes(snapshot.Standby)} · Free {Format.Bytes(snapshot.Free)}";
        ReclaimItem.IsEnabled = !service.IsCleaning;
        PauseItem.Visibility = service.HasAutomation ? Visibility.Visible : Visibility.Collapsed;
        PauseIcon.Text = service.IsPaused ? "" : "";
        PauseText.Text = service.IsPaused ? "Resume automatic cleaning" : "Pause automatic cleaning";
    }

    void OnContentRendered(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        PlaceNearCursor(handle);
        Activate();
        Native.SetForegroundWindow(handle);
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));
    }

    void PlaceNearCursor(IntPtr handle)
    {
        Native.GetCursorPos(out var cursor);
        var monitor = Native.MonitorFromPoint(cursor, Native.MonitorDefaultToNearest);
        var info = new Native.MonitorInfo { Size = Marshal.SizeOf<Native.MonitorInfo>() };
        Native.GetMonitorInfo(monitor, ref info);
        double scale = Native.GetDpiForMonitor(monitor, 0, out uint dpi, out _) == 0 ? dpi / 96d : 1;

        int width = (int)Math.Ceiling(ActualWidth * scale);
        int height = (int)Math.Ceiling(ActualHeight * scale);
        var work = info.Work;

        int x = cursor.X - width / 2;
        x = Math.Max(work.Left, Math.Min(x, work.Right - width));
        int y = cursor.Y - height;
        if (y < work.Top)
            y = cursor.Y;
        y = Math.Max(work.Top, Math.Min(y, work.Bottom - height));

        Native.SetWindowPos(handle, Native.HwndTopmost, x, y, 0, 0, Native.SwpNoSize | Native.SwpNoActivate);
    }

    void Dismiss()
    {
        if (closing)
            return;
        closing = true;
        Close();
    }

    void OnReclaim(object sender, RoutedEventArgs e)
    {
        Dismiss();
        _ = App.Current.CleanFromTrayAsync();
    }

    void OnOpen(object sender, RoutedEventArgs e)
    {
        Dismiss();
        App.Current.ShowMainWindow();
    }

    void OnPause(object sender, RoutedEventArgs e)
    {
        Dismiss();
        service.SetPaused(!service.IsPaused);
    }

    void OnQuit(object sender, RoutedEventArgs e)
    {
        Dismiss();
        App.Current.ExitApplication();
    }
}
