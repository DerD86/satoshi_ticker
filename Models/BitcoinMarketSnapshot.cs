namespace SatoshiTicker.Models;

public sealed record BitcoinMarketSnapshot(
    decimal CurrentPriceEuro,
    decimal PriceTenMinutesAgoEuro,
    DateTimeOffset RetrievedAt)
{
    public decimal ChangePercent => PriceTenMinutesAgoEuro == 0
        ? 0
        : ((CurrentPriceEuro - PriceTenMinutesAgoEuro) / PriceTenMinutesAgoEuro) * 100;
}
