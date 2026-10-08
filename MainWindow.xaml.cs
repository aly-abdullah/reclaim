using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Reclaim.Core;
using Reclaim.Ui;

namespace Reclaim;

public partial class MainWindow : Window
{
    const double HotThreshold = 0.85;

    readonly ReclaimService service;
    readonly double[] inUseBuffer = new double[ReclaimService.HistoryCapacity];
    readonly double[] standbyBuffer = new double[ReclaimService.HistoryCapacity];
    readonly Brush[] barBrushes;
    readonly Brush accentArc;
    readonly Brush hotArc;
    bool hot;

    public MainWindow(ReclaimService service)
    {
        InitializeComponent();
        this.service = service;
        ViewModel = new MainViewModel(service);
        DataContext = ViewModel;

        barBrushes =
        [
            (Brush)FindResource("InUseBrush"),
            (Brush)FindResource("ModifiedBrush"),
            (Brush)FindResource("StandbyBrush"),
            (Brush)FindResource("FreeBrush")
        ];
        accentArc = (Brush)FindResource("AccentGradient");
        hotArc = (Brush)FindResource("HotGradient");

        service.Sampled += (_, _) => RenderLive();
        service.Cleaned += OnCleaned;
        IsVisibleChanged += (_, _) => RenderLive();
        SourceInitialized += (_, _) => ApplyWindowEffects();
        MouseDown += (_, _) => Keyboard.ClearFocus();
        Loaded += (_, _) => RenderLive();
    }

    public MainViewModel ViewModel { get; }

    void RenderLive()
    {
        if (!IsVisible || WindowState == WindowState.Minimized)
            return;

        var snapshot = service.Current;
        Ring.AnimateTo(snapshot.UsedRatio);

        bool nowHot = snapshot.UsedRatio >= HotThreshold;
        if (nowHot != hot || Ring.ArcBrush is null)
        {
            hot = nowHot;
            Ring.ArcBrush = hot ? hotArc : accentArc;
            RingGlow.Color = hot ? Color.FromRgb(0xF2, 0x55, 0x5A) : Color.FromRgb(0x7C, 0x6C, 0xFF);
        }

        Bar.Update(
        [
            snapshot.Fraction(snapshot.InUse),
            snapshot.Fraction(snapshot.Modified),
            snapshot.Fraction(snapshot.Standby),
            snapshot.Fraction(snapshot.Free)
        ], barBrushes);

        int count = service.CopyHistory(inUseBuffer, standbyBuffer);
        Chart.Update(inUseBuffer, standbyBuffer, count, ReclaimService.HistoryCapacity, service.MarkPositions(count));
    }

    void OnCleaned(object? sender, CleanCompletedEventArgs e)
    {
        RenderLive();
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        ResultPanel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320)) { EasingFunction = ease });
        ResultShift.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, new DoubleAnimation(-10, 0, TimeSpan.FromMilliseconds(420)) { EasingFunction = ease });
    }

    void ApplyWindowEffects()
    {
        var handle = new WindowInteropHelper(this).Handle;
        int dark = 1;
        int round = 2;
        int border = 0x00342B25;
        int caption = 0x00120D0B;
        Native.DwmSetWindowAttribute(handle, Native.DwmUseImmersiveDarkMode, ref dark, sizeof(int));
        Native.DwmSetWindowAttribute(handle, Native.DwmWindowCornerPreference, ref round, sizeof(int));
        Native.DwmSetWindowAttribute(handle, Native.DwmBorderColor, ref border, sizeof(int));
        Native.DwmSetWindowAttribute(handle, Native.DwmCaptionColor, ref caption, sizeof(int));
    }

    public void HideToTray()
    {
        Hide();
        if (service.Settings.TrayHintShown)
            return;
        service.Settings.TrayHintShown = true;
        SettingsStore.Save(service.Settings);
        App.Current.Tray.Notify("Reclaim is still running", "It keeps cleaning from the tray. Right-click the icon for options.");
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Minimized && service.Settings.MinimizeToTray)
        {
            HideToTray();
            WindowState = WindowState.Normal;
        }
        else if (WindowState == WindowState.Normal)
        {
            RenderLive();
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!App.Current.IsExiting)
        {
            e.Cancel = true;
            if (service.Settings.CloseToTray)
                HideToTray();
            else
                Dispatcher.BeginInvoke(App.Current.ExitApplication);
            return;
        }
        base.OnClosing(e);
    }

    void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    void OnClose(object sender, RoutedEventArgs e) => Close();
}
