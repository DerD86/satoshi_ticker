using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
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

    private AppSettings _settings = new();
    private bool _isRefreshing;
    private bool _hasLoaded;

    public MainWindow()
    {
        InitializeComponent();

        _refreshTimer = new DispatcherTimer
        {
            Interval = RefreshInterval
        };

        _refreshTimer.Tick += async (_, _) => await RefreshPriceAsync();
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

        double defaultLeft = workArea.Right - Width - 12;
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

        Rect proposedWindow = new(left.Value, top.Value, Width, Height);
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

            PortfolioValueText.Text = euroValue.ToString("N2 '€'", GermanCulture);
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

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
