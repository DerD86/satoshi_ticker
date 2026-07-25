using System.Globalization;
using System.Windows;
using System.Windows.Input;

namespace SatoshiTicker;

public partial class AmountDialog : Window
{
    private static readonly CultureInfo GermanCulture = CultureInfo.GetCultureInfo("de-DE");

    public decimal BitcoinAmount { get; private set; }

    public AmountDialog(decimal currentBitcoinAmount)
    {
        InitializeComponent();

        AmountTextBox.Text = currentBitcoinAmount > 0
            ? currentBitcoinAmount.ToString("0.########", GermanCulture)
            : string.Empty;

        Loaded += (_, _) =>
        {
            AmountTextBox.Focus();
            AmountTextBox.SelectAll();
        };
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string input = AmountTextBox.Text.Trim();

        bool isValid = decimal.TryParse(
            input,
            NumberStyles.Number,
            GermanCulture,
            out decimal amount);

        if (!isValid)
        {
            isValid = decimal.TryParse(
                input.Replace(',', '.'),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out amount);
        }

        if (!isValid || amount < 0 || amount > 21_000_000)
        {
            MessageBox.Show(
                this,
                "Bitte gib einen gültigen BTC-Bestand ein, zum Beispiel 0,00128456.",
                "Ungültiger BTC-Bestand",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            AmountTextBox.Focus();
            AmountTextBox.SelectAll();
            return;
        }

        BitcoinAmount = amount;
        DialogResult = true;
    }

    private void AmountTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Save_Click(sender, e);
        }
    }
}
