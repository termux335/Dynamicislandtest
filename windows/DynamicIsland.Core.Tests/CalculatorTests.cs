using DynamicIsland.Core;

namespace DynamicIsland.Core.Tests;

public class CalculatorTests
{
    [Fact]
    public void ArithmeticRespectsPrecedence()
    {
        Assert.Equal(14, Calculator.Evaluate("2 + 3 * 4").Value);
        Assert.Equal(2, Calculator.Evaluate("(2 + 3) * 4 / 10").Value);
    }

    [Fact]
    public void PercentOfAmountUsesIndonesianPhrase()
    {
        var result = Calculator.Evaluate("15% dari 240 ribu");

        Assert.True(result.Success);
        Assert.Equal(36_000, result.Value);
        Assert.Equal("36.000", result.Display);
    }

    [Fact]
    public void ThousandSeparatorsAndDecimalCommaAreUnderstood()
    {
        Assert.Equal(2000, Calculator.Evaluate("1.500 + 500").Value);
        Assert.Equal(5, Calculator.Evaluate("2,5 * 2").Value);
    }

    [Fact]
    public void LengthConversionWorks()
    {
        var result = Calculator.Evaluate("10 km ke mil");

        Assert.True(result.Success);
        Assert.Equal(6.21, Math.Round(result.Value, 2));
        Assert.Equal("6,21 mil", result.Display);
    }

    [Fact]
    public void TimeConversionWorks()
    {
        Assert.Equal(120, Calculator.Evaluate("2 jam ke menit").Value);
        Assert.Equal(90, Calculator.Evaluate("5400 detik ke menit").Value);
    }

    [Fact]
    public void TemperatureConversionWorks()
    {
        Assert.Equal(212, Math.Round(Calculator.Evaluate("100 c ke f").Value, 4));
        Assert.Equal(0, Math.Round(Calculator.Evaluate("32 f ke c").Value, 4));
    }

    [Fact]
    public void UnknownUnitFailsWithAMessage()
    {
        var result = Calculator.Evaluate("10 km ke bulan");

        Assert.False(result.Success);
        Assert.Contains("tidak dikenal", result.Display, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DivideByZeroFails()
    {
        var result = Calculator.Evaluate("8 / 0");

        Assert.False(result.Success);
        Assert.Contains("nol", result.Display);
    }

    [Fact]
    public void PlainWordsAreNotCalculations()
    {
        Assert.False(Calculator.LooksLikeExpression("belanja besok"));
        Assert.False(Calculator.Evaluate("belanja besok").Success);
    }

    [Fact]
    public void NumberFormatUsesIndonesianSeparators()
    {
        Assert.Equal("1.234.567,5", Calculator.FormatNumber(1_234_567.5));
        Assert.Equal("-2.500", Calculator.FormatNumber(-2500));
        Assert.Equal("0,25", Calculator.FormatNumber(0.25));
    }
}
