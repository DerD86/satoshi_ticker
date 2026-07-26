using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using SatoshiTicker.Models;
using SatoshiTicker.Services;

namespace SatoshiTicker;

public partial class MainWindow : Window
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(10);
    private static readonly CultureInfo GermanCulture = CultureInfo.GetCultureInfo("de-DE");

    private static readonly Brush PositiveBrush =
        new SolidColorBrush(Color.FromRgb(46, 204, 113));

    private static readonly Brush NegativeBrush =
        new SolidColorBrush(Color.FromRgb(231, 76, 60));

    private static readonly Brush NeutralBrush =
        new SolidColorBrush(Color.FromRgb(176, 176, 176));

    private readonly SettingsService _settingsService = new();
    private readonly BitcoinPriceService _priceService = new();
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _topmostRecoveryTimer;

    private AppSettings _settings = new();
    private bool _isRefreshing;
    private bool _hasLoaded;
    private int _remainingTopmostRecoveryAttempts;
    private IntPtr _windowHandle;
    private IntPtr _foregroundEventHook;
    private NativeMethods.WinEventDelegate? _foregroundEventHandler;

    public MainWindow()
    {
        InitializeComponent();

        _refreshTimer = new DispatcherTimer
        {
            Interval = RefreshInterval
        };

        _refreshTimer.Tick += async (_, _) => await RefreshPriceAsync();

        _topmostRecoveryTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };

        _topmostRecoveryTimer.Tick += (_, _) =>
        {
            EnsureTopmost();

            if (--_remainingTopmostRecoveryAttempts <= 0)
            {
                _topmostRecoveryTimer.Stop();
            }
        };
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        _windowHandle = new WindowInteropHelper(this).Handle;

        int extendedStyle = NativeMethods.GetWindowLong(_windowHandle, NativeMethods.GwlExStyle);
        NativeMethods.SetWindowLong(
            _windowHandle,
            NativeMethods.GwlExStyle,
            extendedStyle | NativeMethods.WsExNoActivate);

        _foregroundEventHandler = (_, _, _, _, _, _, _) =>
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, RestoreTopmostAfterForegroundChange);

        _foregroundEventHook = NativeMethods.SetWinEventHook(
            NativeMethods.EventSystemForeground,
            NativeMethods.EventSystemForeground,
            IntPtr.Zero,
            _foregroundEventHandler,
            0,
            0,
            NativeMethods.WineventOutofcontext);

        EnsureTopmost();
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        CloseToolTip();
        Dispatcher.BeginInvoke(DispatcherPriority.Normal, RestoreTopmostAfterForegroundChange);
    }

    private void RestoreTopmostAfterForegroundChange()
    {
        EnsureTopmost();
        _remainingTopmostRecoveryAttempts = 8;
        _topmostRecoveryTimer.Stop();
        _topmostRecoveryTimer.Start();
    }

    private void EnsureTopmost()
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.SetWindowPos(
            _windowHandle,
            NativeMethods.HwndTopmost,
            0,
            0,
            0,
            0,
            NativeMethods.SwpNomove |
            NativeMethods.SwpNosize |
            NativeMethods.SwpNoactivate |
            NativeMethods.SwpShowwindow);
    }

    private void Window_ToolTipOpening(object sender, ToolTipEventArgs e)
    {
        if (_windowHandle == IntPtr.Zero ||
            !NativeMethods.GetCursorPos(out NativeMethods.Point cursorPosition) ||
            NativeMethods.WindowFromPoint(cursorPosition) != _windowHandle)
        {
            e.Handled = true;
        }
    }

    private void CloseToolTip()
    {
        ToolTipService.SetIsEnabled(this, false);
        ToolTipService.SetIsEnabled(this, true);
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _settings = await _settingsService.LoadAsync();
        RestoreOrSetDefaultPosition();
        AutoStartMenuItem.IsChecked = _settings.StartWithWindows;
        _hasLoaded = true;

        if (_settings.BitcoinAmount <= 0 && !await ShowAmountDialogAsync())
        {
            PortfolioValueText.Text = "BTC-Bestand fehlt";
            SetNeutralTrend();
        }

        await RefreshPriceAsync();
        _refreshTimer.Start();
    }

    private void RestoreOrSetDefaultPosition()
    {
        Rect workArea = SystemParameters.WorkArea;

        double defaultLeft = workArea.Right - ActualWidth - 12;
        double defaultTop = workArea.Bottom - Height - 8;

        Left = IsPositionVisible(_settings.WindowLeft, _settings.WindowTop)
            ? _settings.WindowLeft!.Value
            : defaultLeft;

        Top = IsPositionVisible(_settings.WindowLeft, _settings.WindowTop)
            ? _settings.WindowTop!.Value
            : defaultTop;
    }

    private bool IsPositionVisible(double? left, double? top)
    {
        if (left is null || top is null)
        {
            return false;
        }

        Rect virtualScreen = new(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);

        Rect proposedWindow = new(left.Value, top.Value, ActualWidth, Height);
        return virtualScreen.IntersectsWith(proposedWindow);
    }

    private async Task RefreshPriceAsync()
    {
        if (_isRefreshing || _settings.BitcoinAmount <= 0)
        {
            return;
        }

        _isRefreshing = true;

        try
        {
            PortfolioValueText.Text = "Lade Kurs …";

            BitcoinMarketSnapshot snapshot = await _priceService.GetSnapshotAsync();
            decimal euroValue = snapshot.CurrentPriceEuro * _settings.BitcoinAmount;

            PortfolioValueText.Text = $"{euroValue.ToString("N2", GermanCulture)} €";
            UpdateTrend(snapshot.ChangePercent);

            ToolTip = string.Join(
                Environment.NewLine,
                $"Bestand: {_settings.BitcoinAmount.ToString("0.########", GermanCulture)} BTC",
                $"BTC-Kurs: {snapshot.CurrentPriceEuro.ToString("N2", GermanCulture)} €",
                $"10-Minuten-Kurs: {snapshot.PriceTenMinutesAgoEuro.ToString("N2", GermanCulture)} €",
                $"Aktualisiert: {snapshot.RetrievedAt.LocalDateTime:HH:mm:ss}");
        }
        catch (Exception exception)
        {
            PortfolioValueText.Text = "Kurs nicht verfügbar";
            TrendArrowText.Text = "!";
            TrendPercentText.Text = string.Empty;
            TrendArrowText.Foreground = NegativeBrush;
            ToolTip = $"Die Aktualisierung ist fehlgeschlagen:{Environment.NewLine}{exception.Message}";
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private void UpdateTrend(decimal changePercent)
    {
        const decimal neutralThreshold = 0.005m;

        if (changePercent > neutralThreshold)
        {
            SetTrend("▲", $" +{changePercent.ToString("N2", GermanCulture)} %", PositiveBrush);
        }
        else if (changePercent < -neutralThreshold)
        {
            SetTrend("▼", $" {changePercent.ToString("N2", GermanCulture)} %", NegativeBrush);
        }
        else
        {
            SetNeutralTrend();
        }
    }

    private void SetNeutralTrend()
    {
        SetTrend("•", " 0,00 %", NeutralBrush);
    }

    private void SetTrend(string arrow, string percentage, Brush brush)
    {
        TrendArrowText.Text = arrow;
        TrendPercentText.Text = percentage;
        TrendArrowText.Foreground = brush;
        TrendPercentText.Foreground = brush;
    }

    private async void ChangeBitcoinAmount_Click(object sender, RoutedEventArgs e)
    {
        if (await ShowAmountDialogAsync())
        {
            await RefreshPriceAsync();
        }
    }

    private async Task<bool> ShowAmountDialogAsync()
    {
        AmountDialog dialog = new(_settings.BitcoinAmount)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return false;
        }

        _settings.BitcoinAmount = dialog.BitcoinAmount;
        await _settingsService.SaveAsync(_settings);
        return true;
    }

    private async void RefreshNow_Click(object sender, RoutedEventArgs e)
    {
        await RefreshPriceAsync();
        _refreshTimer.Stop();
        _refreshTimer.Start();
    }

    private async void AutoStart_Click(object sender, RoutedEventArgs e)
    {
        bool requestedState = AutoStartMenuItem.IsChecked;

        try
        {
            AutoStartService.SetEnabled(requestedState);
            _settings.StartWithWindows = requestedState;
            await _settingsService.SaveAsync(_settings);
        }
        catch (Exception exception)
        {
            AutoStartMenuItem.IsChecked = !requestedState;

            MessageBox.Show(
                this,
                exception.Message,
                "Autostart konnte nicht geändert werden",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private async void Window_LocationChanged(object? sender, EventArgs e)
    {
        if (!_hasLoaded)
        {
            return;
        }

        _settings.WindowLeft = Left;
        _settings.WindowTop = Top;

        try
        {
            await _settingsService.SaveAsync(_settings);
        }
        catch
        {
            // Eine fehlgeschlagene Positionsspeicherung darf die App nicht beenden.
        }
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        _refreshTimer.Stop();
        _topmostRecoveryTimer.Stop();

        if (_foregroundEventHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_foregroundEventHook);
            _foregroundEventHook = IntPtr.Zero;
        }

        _settings.WindowLeft = Left;
        _settings.WindowTop = Top;

        try
        {
            await _settingsService.SaveAsync(_settings);
        }
        catch
        {
            // Beim Beenden ist keine weitere Benutzeraktion nötig.
        }
    }

    private static class NativeMethods
    {
        internal const int EventSystemForeground = 0x0003;
        internal const int WineventOutofcontext = 0;
        internal const int GwlExStyle = -20;
        internal const int WsExNoActivate = 0x08000000;
        internal const uint SwpNosize = 0x0001;
        internal const uint SwpNomove = 0x0002;
        internal const uint SwpNoactivate = 0x0010;
        internal const uint SwpShowwindow = 0x0040;
        internal static readonly IntPtr HwndTopmost = new(-1);

        internal delegate void WinEventDelegate(
            IntPtr eventHook,
            uint eventType,
            IntPtr windowHandle,
            int objectId,
            int childId,
            uint eventThread,
            uint eventTime);

        [StructLayout(LayoutKind.Sequential)]
        internal struct Point
        {
            internal int X;
            internal int Y;
        }

        [DllImport("user32.dll")]
        internal static extern int GetWindowLong(IntPtr windowHandle, int index);

        [DllImport("user32.dll")]
        internal static extern int SetWindowLong(IntPtr windowHandle, int index, int newLong);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWindowPos(
            IntPtr windowHandle,
            IntPtr insertAfter,
            int x,
            int y,
            int width,
            int height,
            uint flags);

        [DllImport("user32.dll")]
        internal static extern IntPtr SetWinEventHook(
            uint eventMin,
            uint eventMax,
            IntPtr eventHookModule,
            WinEventDelegate eventHandler,
            uint processId,
            uint threadId,
            uint flags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnhookWinEvent(IntPtr eventHook);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(out Point point);

        [DllImport("user32.dll")]
        internal static extern IntPtr WindowFromPoint(Point point);
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
