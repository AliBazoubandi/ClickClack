using System.Diagnostics;
using System.Windows.Media;
using FontFamily = System.Windows.Media.FontFamily;

namespace PixelCompanion.Services;

public static class FontHelper
{
    public const string FallbackFontFamilyString = "Consolas, Segoe UI, Tahoma, sans-serif";

    /// <summary>
    /// Checks whether at least one typeface in the specified FontFamily can resolve a glyph for the specified test character.
    /// </summary>
    public static bool CanResolveGlyph(FontFamily? fontFamily, char testChar = 'ت')
    {
        if (fontFamily == null) return false;

        try
        {
            var typefaces = fontFamily.GetTypefaces();
            foreach (var typeface in typefaces)
            {
                if (typeface.TryGetGlyphTypeface(out var glyphTypeface) && glyphTypeface != null)
                {
                    if (glyphTypeface.CharacterToGlyphMap.ContainsKey(testChar))
                    {
                        return true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Font resolution exception: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    /// Verifies the primary font family resolves. If resolution fails, falls back to the system chain
    /// (Consolas/Segoe UI) without crashing, and emits a Diagnostic Debug.WriteLine naming the missing face.
    /// </summary>
    public static FontFamily EnsureOrFallback(FontFamily primaryFamily, string faceNameForLog = "Mikhak-FD VF", char testChar = 'ت')
    {
        if (CanResolveGlyph(primaryFamily, testChar))
        {
            return primaryFamily;
        }

        Debug.WriteLine($"Warning: Failed to resolve embedded font face '{faceNameForLog}'. Falling back to system font chain: {FallbackFontFamilyString}");
        return new FontFamily(FallbackFontFamilyString);
    }
}
