using System;
using System.Runtime.InteropServices;
using System.Text;

namespace AutoAdventurer.Diagnostics;

internal static class LogText
{
    private const uint ChineseCodePage = 936;
    private const uint NoBestFitCharacters = 0x00000400;
    private static readonly Encoding Utf8Strict =
        new UTF8Encoding(false, true);
    private static readonly Encoding ChineseLegacy = CreateChineseEncoding();

    internal static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        string recovered = TryRecoverWithManagedEncoding(value);
        if (!string.Equals(recovered, value, StringComparison.Ordinal))
            return recovered;

        return TryRecoverWithWindowsCodePage(value);
    }

    private static string TryRecoverWithManagedEncoding(string value)
    {
        if (ChineseLegacy == null)
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

    private static string TryRecoverWithWindowsCodePage(string value)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return value;

        try
        {
            byte[] bytes = EncodeWindowsCodePage(value);
            if (bytes == null || bytes.Length == 0)
                return value;

            string recovered = Utf8Strict.GetString(bytes);
            if (string.Equals(recovered, value, StringComparison.Ordinal))
                return value;

            byte[] utf8 = Encoding.UTF8.GetBytes(recovered);
            string roundTrip = DecodeWindowsCodePage(utf8);
            return string.Equals(roundTrip, value, StringComparison.Ordinal)
                ? recovered
                : value;
        }
        catch
        {
            return value;
        }
    }

    private static byte[] EncodeWindowsCodePage(string value)
    {
        bool usedDefaultCharacter;
        int byteCount = WideCharToMultiByte(
            ChineseCodePage, NoBestFitCharacters, value, value.Length,
            null, 0, null, out usedDefaultCharacter);
        if (byteCount <= 0 || usedDefaultCharacter)
            return null;

        var bytes = new byte[byteCount];
        int written = WideCharToMultiByte(
            ChineseCodePage, NoBestFitCharacters, value, value.Length,
            bytes, bytes.Length, null, out usedDefaultCharacter);
        return written == bytes.Length && !usedDefaultCharacter
            ? bytes
            : null;
    }

    private static string DecodeWindowsCodePage(byte[] bytes)
    {
        int characterCount = MultiByteToWideChar(
            ChineseCodePage, 0, bytes, bytes.Length, null, 0);
        if (characterCount <= 0)
            return null;

        var characters = new char[characterCount];
        int written = MultiByteToWideChar(
            ChineseCodePage, 0, bytes, bytes.Length,
            characters, characters.Length);
        return written == characters.Length
            ? new string(characters)
            : null;
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

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode,
        ExactSpelling = true, SetLastError = true)]
    private static extern int WideCharToMultiByte(
        uint codePage,
        uint flags,
        string wideCharacters,
        int wideCharacterCount,
        [Out] byte[] multiByteCharacters,
        int multiByteCharacterCapacity,
        byte[] defaultCharacter,
        out bool usedDefaultCharacter);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern int MultiByteToWideChar(
        uint codePage,
        uint flags,
        byte[] multiByteCharacters,
        int multiByteCharacterCount,
        [Out] char[] wideCharacters,
        int wideCharacterCapacity);
}
