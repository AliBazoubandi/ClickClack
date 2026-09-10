using System.Globalization;
using System.Windows;
using System.Windows.Data;
using FlowDirection = System.Windows.FlowDirection;

namespace PixelCompanion.Converters;

public class TextToFlowDirectionConverter : IValueConverter
{
    public static FlowDirection GetFlowDirection(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return FlowDirection.LeftToRight;

        // Skip leading numbers, punctuation, spaces, and symbols to inspect the first strong directional character
        foreach (char ch in text)
        {
            if (char.IsWhiteSpace(ch) || char.IsPunctuation(ch) || char.IsDigit(ch) || char.IsSymbol(ch))
                continue;

            if (IsRtlChar(ch))
                return FlowDirection.RightToLeft;

            return FlowDirection.LeftToRight;
        }

        return FlowDirection.LeftToRight;
    }

    public static bool IsRtlChar(char ch)
    {
        return (ch >= 0x0600 && ch <= 0x06FF) || // Arabic / Persian
               (ch >= 0x0750 && ch <= 0x077F) || // Arabic Supplement
               (ch >= 0x08A0 && ch <= 0x08FF) || // Arabic Extended-A
               (ch >= 0xFB50 && ch <= 0xFDFF) || // Arabic Presentation Forms-A
               (ch >= 0xFE70 && ch <= 0xFEFF) || // Arabic Presentation Forms-B
               (ch >= 0x0590 && ch <= 0x05FF);   // Hebrew
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value as string;
        return GetFlowDirection(text);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
