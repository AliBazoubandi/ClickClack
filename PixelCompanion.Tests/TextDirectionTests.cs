using System.Windows;
using PixelCompanion.Converters;

namespace PixelCompanion.Tests;

[TestClass]
public class TextDirectionTests
{
    private readonly TextToFlowDirectionConverter _converter = new();

    [TestMethod]
    public void Test_PersianText_ReturnsRightToLeft()
    {
        Assert.AreEqual(FlowDirection.RightToLeft, TextToFlowDirectionConverter.GetFlowDirection("خرید نان و پنیر"));
        Assert.AreEqual(FlowDirection.RightToLeft, TextToFlowDirectionConverter.GetFlowDirection("انجام وظیفه شماره ۱"));
        Assert.AreEqual(FlowDirection.RightToLeft, TextToFlowDirectionConverter.GetFlowDirection("سلام دنیا!"));
        Assert.AreEqual(FlowDirection.RightToLeft, TextToFlowDirectionConverter.GetFlowDirection("گزارش ماهانه (نهایی)"));
    }

    [TestMethod]
    public void Test_EnglishText_ReturnsLeftToRight()
    {
        Assert.AreEqual(FlowDirection.LeftToRight, TextToFlowDirectionConverter.GetFlowDirection("Buy milk and eggs"));
        Assert.AreEqual(FlowDirection.LeftToRight, TextToFlowDirectionConverter.GetFlowDirection("Fix bug #123"));
        Assert.AreEqual(FlowDirection.LeftToRight, TextToFlowDirectionConverter.GetFlowDirection("Hello world!"));
    }

    [TestMethod]
    public void Test_PrefixedText_InspectsFirstStrongCharacter()
    {
        // Leading digits and punctuation should not interfere with Persian detection
        Assert.AreEqual(FlowDirection.RightToLeft, TextToFlowDirectionConverter.GetFlowDirection("1. خرید نان"));
        Assert.AreEqual(FlowDirection.LeftToRight, TextToFlowDirectionConverter.GetFlowDirection("1. Buy milk"));

        // Markdown checklist formats
        Assert.AreEqual(FlowDirection.RightToLeft, TextToFlowDirectionConverter.GetFlowDirection("- [ ] تسک جدید روزانه"));
        Assert.AreEqual(FlowDirection.LeftToRight, TextToFlowDirectionConverter.GetFlowDirection("- [ ] New daily task"));
    }

    [TestMethod]
    public void Test_MixedText_FollowsFirstStrongCharacter()
    {
        // Starts with Persian, contains English
        Assert.AreEqual(FlowDirection.RightToLeft, TextToFlowDirectionConverter.GetFlowDirection("بررسی پول ریکوئست در GitHub"));

        // Starts with English, contains Persian
        Assert.AreEqual(FlowDirection.LeftToRight, TextToFlowDirectionConverter.GetFlowDirection("Review GitHub PR برای پروژه"));
    }

    [TestMethod]
    public void Test_NullOrWhitespaceOrNeutral_ReturnsLeftToRight()
    {
        Assert.AreEqual(FlowDirection.LeftToRight, TextToFlowDirectionConverter.GetFlowDirection(null));
        Assert.AreEqual(FlowDirection.LeftToRight, TextToFlowDirectionConverter.GetFlowDirection(""));
        Assert.AreEqual(FlowDirection.LeftToRight, TextToFlowDirectionConverter.GetFlowDirection("   "));
        Assert.AreEqual(FlowDirection.LeftToRight, TextToFlowDirectionConverter.GetFlowDirection("12345"));
        Assert.AreEqual(FlowDirection.LeftToRight, TextToFlowDirectionConverter.GetFlowDirection("!@#$%^&*()"));
    }

    [TestMethod]
    public void Test_ConverterConvertMethod()
    {
        var resultRtl = _converter.Convert("تست فارسی", typeof(FlowDirection), null, System.Globalization.CultureInfo.InvariantCulture);
        Assert.AreEqual(FlowDirection.RightToLeft, resultRtl);

        var resultLtr = _converter.Convert("English test", typeof(FlowDirection), null, System.Globalization.CultureInfo.InvariantCulture);
        Assert.AreEqual(FlowDirection.LeftToRight, resultLtr);
    }

    [TestMethod]
    public void Test_ConvertBack_ReturnsBindingDoNothing_NeverThrows()
    {
        var result = _converter.ConvertBack(FlowDirection.RightToLeft, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture);
        Assert.AreEqual(System.Windows.Data.Binding.DoNothing, result);

        var resultNull = _converter.ConvertBack(null, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture);
        Assert.AreEqual(System.Windows.Data.Binding.DoNothing, resultNull);
    }
}
