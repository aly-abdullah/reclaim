using Microsoft.Win32;
using Reclaim.Core;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace Reclaim.Ui;

public sealed class TrayController : IDisposable
{
    readonly App app;
    readonly ReclaimService service;
    readonly Forms.NotifyIcon notifyIcon;
    Drawing.Icon? icon;
    TrayMenu? menu;
    int renderedPercent = -1;
    bool renderedLight;
    bool lightTaskbar;
    bool disposed;

    public TrayController(App app, ReclaimService service)
    {
        this.app = app;
        this.service = service;
        lightTaskbar = TrayIconRenderer.TaskbarIsLight();

        notifyIcon = new Forms.NotifyIcon { Text = "Reclaim" };
        notifyIcon.MouseUp += OnMouseUp;
        notifyIcon.BalloonTipClicked += (_, _) => app.ShowMainWindow();
        service.Sampled += OnSampled;
        service.StateChanged += OnSampled;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

        Refresh();
        notifyIcon.Visible = true;
    }

    public void Notify(string title, string text)
    {
        if (!disposed)
            notifyIcon.ShowBalloonTip(4000, title, text, Forms.ToolTipIcon.None);
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        service.Sampled -= OnSampled;
        service.StateChanged -= OnSampled;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        menu?.Close();
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
        icon?.Dispose();
    }

    void OnSampled(object? sender, EventArgs e) => Refresh();

    void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General)
            return;
        lightTaskbar = TrayIconRenderer.TaskbarIsLight();
        Refresh();
    }

    void OnMouseUp(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button == Forms.MouseButtons.Left)
            app.ShowMainWindow();
        else if (e.Button == Forms.MouseButtons.Right)
            ShowMenu();
    }

    void ShowMenu()
    {
        if (menu is not null)
        {
            menu.Close();
            return;
        }
        menu = new TrayMenu(service);
        menu.Closed += (_, _) => menu = null;
        menu.Show();
    }

    void Refresh()
    {
        if (disposed)
            return;

        var snapshot = service.Current;
        int percent = (int)Math.Round(snapshot.UsedRatio * 100);
        if (percent != renderedPercent || lightTaskbar != renderedLight)
        {
            int size = Math.Max(16, Forms.SystemInformation.SmallIconSize.Width);
            var next = TrayIconRenderer.Render(snapshot.UsedRatio, lightTaskbar, size);
            notifyIcon.Icon = next;
            icon?.Dispose();
            icon = next;
            renderedPercent = percent;
            renderedLight = lightTaskbar;
        }

        string state = service.IsPaused ? " · paused" : "";
        string text = $"Reclaim{state}\n{percent}% in use · Standby {Format.Bytes(snapshot.Standby)} · Free {Format.Bytes(snapshot.Free)}";
        notifyIcon.Text = text.Length > 127 ? text[..127] : text;
    }
}
