using System.Globalization;
using System.Windows.Controls;
using PixelCompanion.Services;

namespace PixelCompanion.Converters;

public class ReminderMinutesValidationRule : ValidationRule
{
    public override ValidationResult Validate(object value, CultureInfo cultureInfo)
    {
        string? text = value as string;
        if (string.IsNullOrWhiteSpace(text))
        {
            return new ValidationResult(false, "Minutes value is required.");
        }

        string normalized = ObsidianTaskParser.NormalizeDigits(text.Trim());
        if (!int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out int minutes))
        {
            return new ValidationResult(false, "Minutes must be a valid number.");
        }

        if (minutes < 0 || minutes > 120)
        {
            return new ValidationResult(false, "Minutes must be between 0 and 120.");
        }

        return ValidationResult.ValidResult;
    }
}
