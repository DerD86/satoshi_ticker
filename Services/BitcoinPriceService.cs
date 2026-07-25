using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using SatoshiTicker.Models;

namespace SatoshiTicker.Services;

public sealed class BitcoinPriceService
{
  private static readonly HttpClient HttpClient = CreateHttpClient();
  private const string OhlcUrl = "https://api.kraken.com/0/public/OHLC?pair=XBTEUR&interval=1";

  public async Task<BitcoinMarketSnapshot> GetSnapshotAsync(
      CancellationToken cancellationToken = default)
  {
    using HttpResponseMessage response = await HttpClient.GetAsync(OhlcUrl, cancellationToken);
    response.EnsureSuccessStatusCode();

    await using Stream responseStream =
        await response.Content.ReadAsStreamAsync(cancellationToken);

    using JsonDocument document = await JsonDocument.ParseAsync(
        responseStream,
        cancellationToken: cancellationToken);

    JsonElement root = document.RootElement;
    ValidateApiErrors(root);

    JsonElement result = root.GetProperty("result");
    JsonElement candles = FindCandleArray(result);

    List<(DateTimeOffset Time, decimal Close)> samples = [];

    foreach (JsonElement candle in candles.EnumerateArray())
    {
      long unixTime = candle[0].GetInt64();
      string closeText = candle[4].GetString()
          ?? throw new InvalidOperationException("Ein Kurswert fehlt in der API-Antwort.");

      if (!decimal.TryParse(
              closeText,
              NumberStyles.Float,
              CultureInfo.InvariantCulture,
              out decimal closePrice))
      {
        continue;
      }

      samples.Add((DateTimeOffset.FromUnixTimeSeconds(unixTime), closePrice));
    }

    if (samples.Count < 2)
    {
      throw new InvalidOperationException("Die Kurs-API hat nicht genügend Messwerte geliefert.");
    }

    samples.Sort((left, right) => left.Time.CompareTo(right.Time));

    (DateTimeOffset currentTime, decimal currentPrice) = samples[^1];
    DateTimeOffset targetTime = currentTime.AddMinutes(-10);

    (DateTimeOffset Time, decimal Close) reference = samples
        .Where(sample => sample.Time <= targetTime)
        .LastOrDefault();

    if (reference == default)
    {
      reference = samples[0];
    }

    return new BitcoinMarketSnapshot(
        currentPrice,
        reference.Close,
        DateTimeOffset.Now);
  }

  private static HttpClient CreateHttpClient()
  {
    HttpClient client = new()
    {
      Timeout = TimeSpan.FromSeconds(15)
    };

    client.DefaultRequestHeaders.UserAgent.ParseAdd("SatoshiTicker/1.0");
    return client;
  }

  private static void ValidateApiErrors(JsonElement root)
  {
    if (!root.TryGetProperty("error", out JsonElement errors)
        || errors.ValueKind != JsonValueKind.Array
        || errors.GetArrayLength() == 0)
    {
      return;
    }

    string errorText = string.Join(", ",
        errors.EnumerateArray().Select(item => item.GetString()));

    throw new InvalidOperationException($"Die Kurs-API meldet einen Fehler: {errorText}");
  }

  private static JsonElement FindCandleArray(JsonElement result)
  {
    foreach (JsonProperty property in result.EnumerateObject())
    {
      if (!string.Equals(property.Name, "last", StringComparison.OrdinalIgnoreCase)
          && property.Value.ValueKind == JsonValueKind.Array)
      {
        return property.Value;
      }
    }

    throw new InvalidOperationException("Die Kursdaten fehlen in der API-Antwort.");
  }
}
