using System.Windows.Media;
using PixelCompanion.Services;

namespace PixelCompanion.Tests;

[TestClass]
public class FontHelperTests
{
    [TestMethod]
    public void Test_EnsureOrFallback_WithBogusFamilyName_ReturnsFallback()
    {
        var bogusFamily = new FontFamily("pack://application:,,,/ClickClack;component/Assets/Fonts/#BogusNonExistentFont");
        var result = FontHelper.EnsureOrFallback(bogusFamily, "BogusNonExistentFont", 'ت');

        Assert.AreEqual(FontHelper.FallbackFontFamilyString, result.Source);
    }

    [TestMethod]
    public void Test_EnsureOrFallback_WithRealKey_ResolvesOrInconclusiveHeadless()
    {
        FontFamily realFamily;
        try
        {
            realFamily = new FontFamily("pack://application:,,,/ClickClack;component/Assets/Fonts/#Mikhak-FD VF, pack://application:,,,/ClickClack;component/Assets/Fonts/#Mikhak-FD");
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"Headless test runner could not initialize FontFamily: {ex.Message}");
            return;
        }

        bool canResolve = FontHelper.CanResolveGlyph(realFamily, 'ت');
        if (!canResolve)
        {
            Assert.Inconclusive("Pack URI embedded font could not be resolved in headless test runner environment.");
            return;
        }

        var result = FontHelper.EnsureOrFallback(realFamily, "Mikhak-FD VF", 'ت');
        Assert.AreSame(realFamily, result);
    }
}
