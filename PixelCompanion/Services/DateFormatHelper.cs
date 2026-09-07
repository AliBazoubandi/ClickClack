namespace PixelCompanion.Services;

public static class DateFormatHelper
{
    public const string DefaultDateFormat = "yyyy-MM-dd";

    public static bool TryFormatDate(string? format, DateTime date, out string result)
    {
        if (!string.IsNullOrWhiteSpace(format))
        {
            try
            {
                result = date.ToString(format);
                return true;
            }
            catch (FormatException)
            {
            }
            catch (ArgumentException)
            {
            }
        }

        result = date.ToString(DefaultDateFormat);
        return false;
    }

    public static string FormatDate(string? format, DateTime date)
    {
        TryFormatDate(format, date, out var result);
        return result;
    }

    public static bool IsValidDateFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return false;
        }

        try
        {
            _ = DateTime.Now.ToString(format);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
