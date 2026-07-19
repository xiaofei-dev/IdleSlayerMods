using System;
using System.Text;

namespace AutoAdventurer.Diagnostics;

internal static class LogText
{
    private static readonly Encoding Utf8Strict =
        new UTF8Encoding(false, true);
    private static readonly Encoding ChineseLegacy = CreateChineseEncoding();

    internal static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || ChineseLegacy == null)
            return value;

        try
        {
            // Some localized strings arrive as UTF-8 bytes that the game's
            // legacy Chinese layer decoded as CP936. Reverse that exact
            // round-trip only when every character is representable and the
            // recovered UTF-8 is valid. Correct Chinese, English, Japanese,
            // and other localization strings are otherwise left untouched.
            byte[] bytes = ChineseLegacy.GetBytes(value);
            string recovered = Utf8Strict.GetString(bytes);
            if (string.Equals(recovered, value, StringComparison.Ordinal))
                return value;
            string roundTrip = ChineseLegacy.GetString(
                Encoding.UTF8.GetBytes(recovered));
            return string.Equals(roundTrip, value, StringComparison.Ordinal)
                ? recovered
                : value;
        }
        catch
        {
            return value;
        }
    }

    private static Encoding CreateChineseEncoding()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(936,
                EncoderFallback.ExceptionFallback,
                DecoderFallback.ExceptionFallback);
        }
        catch
        {
            return null;
        }
    }
}
