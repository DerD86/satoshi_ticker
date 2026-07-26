namespace SatoshiTicker.Models;

public sealed class AppSettings
{
    public decimal BitcoinAmount { get; set; }
    public bool StartWithWindows { get; set; }
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public string Theme { get; set; } = "Dark";
}
